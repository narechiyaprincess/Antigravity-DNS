using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgDns {
 public sealed class GuardResult {
  public bool Allowed;public string Code;public string Detail;public double Seconds;
 }

 // never start a mutation while the Windows CIM/WMI layer does not answer, and never leave an unfinished restore without telling
 // the user exactly what is still there. Nothing in this file writes to the registry or calls a CIM cmdlet: the manual instruction is text only.
 public static class Recovery {
  public const string WmiNotResponding="WMI_NOT_RESPONDING";
  public const string RestoreUnconfirmed="RESTORE_UNCONFIRMED";
  const string PolicyKey=@"HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig\";
  const string DohKey=@"HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DohWellKnownServers\";
  static readonly Regex GuidName=new Regex(@"^\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?$",RegexOptions.Compiled);
  static readonly Regex OwnerId=new Regex("^[a-f0-9]{32}$",RegexOptions.Compiled);

  public static string StatePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"AntigravityDnsUnlocker","state.json");

  // Attempts of the pre-check. A cold CIM provider (first query after boot or after an antivirus scan) can need more than ten seconds once and then answer in one:
  // one retry separates "slow once" from "hung", while a genuinely hung WMI costs two short deadlines instead of one.
  public const int GuardAttempts=2;

  static string CodeOf(string output) {
   try{using(var doc=JsonDocument.Parse(output)){if(doc.RootElement.TryGetProperty("Code",out var c)&&c.ValueKind==JsonValueKind.String)return c.GetString();}}catch{}
   return null;
  }

  // The unelevated pre-check: a read-only engine `status` with a short deadline. The Engine runs it through the same embedded, hash-verified script.
  // CIM answers (any JSON status, even "not configured") -> allowed. Timeout (after GuardAttempts tries) -> WMI_NOT_RESPONDING; any other engine failure is reported as is and not retried.
  // `run` is the seam for tests: it runs one status attempt with the given deadline. A caller's cancellation propagates as OperationCanceledException (it is not a hung WMI).
  public static async Task<GuardResult> CheckCimAsync(string[] servers,string template,string profileName,CancellationToken ct,TimeSpan? deadline=null,Func<TimeSpan,Task<(int Code,string Output)>> run=null) {
   var sw=System.Diagnostics.Stopwatch.StartNew();
   var limit=deadline??Program.GuardDeadline;
   run=run??(d=>Program.Engine("status",false,false,servers,template,profileName,ct,d));
   (int Code,string Output) result=default;string code=null;int attempts=0;
   while(attempts<GuardAttempts) {
    ct.ThrowIfCancellationRequested();attempts++;
    result=await run(limit);code=CodeOf(result.Output);
    if(!(result.Code==1460||code=="ENGINE_TIMEOUT"))break;
   }
   var g=new GuardResult{Seconds=Math.Round(sw.Elapsed.TotalSeconds,1)};
   if(result.Code==0){g.Allowed=true;g.Code=code??"";return g;}
   g.Code=result.Code==1460||code=="ENGINE_TIMEOUT"?WmiNotResponding:(string.IsNullOrWhiteSpace(code)?"ENGINE_ERROR":code);
   g.Detail=g.Code==WmiNotResponding?"The CIM/WMI status check did not answer within "+(int)limit.TotalSeconds+" s ("+attempts+" attempts); no change was started.":(code??"engine failed");
   return g;
  }

  // The journal phase read straight from state.json (readable by every user), for the moments when the engine's own status cannot answer (WMI hangs):
  // the window must still know that a restore is pending and keep "Восстановить" available. Null = no readable journal.
  public static string PhaseOfJournal(string journalJson) {
   if(string.IsNullOrWhiteSpace(journalJson))return null;
   try{using(var doc=JsonDocument.Parse(journalJson)){string ph=Str(doc.RootElement,"Phase");return ph=="Applying"||ph=="Active"||ph=="Restoring"||ph=="Restored"||ph=="Failed"?ph:null;}}catch{return null;}
  }

  // Text for the user, generated only from the ownership journal: the exact registry keys the program created, whether they are safe to remove, and the reboot note.
  // Returns null when the journal is absent, unreadable or already Restored.
  public static string BuildInstruction(string journalJson) {
   if(string.IsNullOrWhiteSpace(journalJson))return null;
   JsonElement root;
   try{root=JsonDocument.Parse(journalJson).RootElement.Clone();}catch{return null;}
   if(root.ValueKind!=JsonValueKind.Object)return null;
   string phase=Str(root,"Phase"),owner=Str(root,"Owner");
   if(phase==null||phase=="Restored"||owner==null||!OwnerId.IsMatch(owner))return null;
   var sb=new StringBuilder();
   sb.AppendLine("Автоматическое восстановление не подтверждено. Что осталось по журналу (фаза: "+phase+"):");
   sb.AppendLine();
   var ruleLines=new List<string>();
   if(root.TryGetProperty("Rules",out var rules)&&rules.ValueKind==JsonValueKind.Array)
    foreach(var r in rules.EnumerateArray()) {
     string name=Str(r,"Name"),ns=Str(r,"Namespace");
     if(!string.IsNullOrEmpty(name)&&GuidName.IsMatch(name))ruleLines.Add("  "+PolicyKey+"{"+name.Trim('{','}').ToLowerInvariant()+"}   (правило для "+(ns??"?")+")");
     else if(string.IsNullOrEmpty(name))ruleLines.Add("  правило для "+(ns??"?")+": имя в журнале не записано. Найдите в том же разделе "+PolicyKey+" ключ, у которого значение Comment = AgDns:"+owner+".");
    }
   sb.AppendLine("1. Правила NRPT. Удаляйте ключ целиком, только если у него Comment = AgDns:"+owner+" и DisplayName = AgDns "+owner+" (чужие правила не трогайте):");
   if(ruleLines.Count==0)sb.AppendLine("  (в журнале нет записанных правил)");else foreach(string l in ruleLines)sb.AppendLine(l);
   sb.AppendLine();
   var owned=new List<string>();var uncertain=new List<string>();
   if(root.TryGetProperty("Doh",out var doh)&&doh.ValueKind==JsonValueKind.Array)
    foreach(var d in doh.EnumerateArray()) {
     string ip=Str(d,"Server"),own=Str(d,"Ownership");bool created=d.TryGetProperty("Created",out var cr)&&cr.ValueKind==JsonValueKind.True;
     if(ip==null||!IPAddress.TryParse(ip,out var parsed)||parsed.ToString()!=ip||!created)continue;
     if(own=="Owned")owned.Add("  "+DohKey+ip);else if(own=="Pending")uncertain.Add("  "+DohKey+ip);
    }
   sb.AppendLine("2. DoH-записи, созданные программой (Ownership=Owned). Удаляйте ключ, только если шаблон в нём совпадает с адресом провайдера из журнала и на этот IP не ссылается другое правило:");
   if(owned.Count==0)sb.AppendLine("  (нет)");else foreach(string l in owned)sb.AppendLine(l);
   if(uncertain.Count>0){sb.AppendLine("  Неоднозначные (Ownership=Pending: неизвестно, создала ли их программа). Без проверки не удаляйте:");foreach(string l in uncertain)sb.AppendLine(l);}
   sb.AppendLine();
   sb.AppendLine("3. Перезагрузите компьютер: DNS-клиент может продолжать использовать удалённые правила, пока Windows не перезагружена.");
   sb.AppendLine("4. После перезагрузки нажмите «Восстановить»: программа сверит журнал и закроет его, когда её записей не останется.");
   sb.AppendLine("Записи, которых нет в списке (адаптеры, hosts, VPN, чужие правила и DoH), программа не создавала.");
   return sb.ToString();
  }
  static string Str(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;

  public static string ReadJournal(string path=null) {
   try{path=path??StatePath;if(!File.Exists(path))return null;using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sr=new StreamReader(fs,Encoding.UTF8))return sr.ReadToEnd();}catch{return null;}
  }

  // The result shown instead of a bare timeout when a restore could not be confirmed. State stays "Failed (restorable)": the journal is unchanged and a new restore is always allowed.
  public static string UnconfirmedJson(string reason,string journalJson) {
   string instruction=BuildInstruction(journalJson);
   return JsonSerializer.Serialize(new{State="Failed (restorable)",Code=RestoreUnconfirmed,Reason=reason,Instruction=instruction,Note="Restore was not confirmed within the time limit. The journal is unchanged; restore can be started again at any time."},Program.JsonOptions);
  }
  public static string BlockedJson(string action,GuardResult g,string journalJson) {
   if(action=="restore"){
    string instruction=BuildInstruction(journalJson);
    if(instruction!=null)return JsonSerializer.Serialize(new{State="Failed (restorable)",Code=RestoreUnconfirmed,Reason=g.Code,Instruction=instruction,Note="Restore was not started because the CIM/WMI layer does not answer; nothing was changed."},Program.JsonOptions);
   }
   return JsonSerializer.Serialize(new{State="Failed (restorable)",Code=g.Code,Detail=g.Detail,Note="No change was started."},Program.JsonOptions);
  }
 }
}
