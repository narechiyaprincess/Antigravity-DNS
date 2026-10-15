using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgDns {
 // Everything in this file is read-only with respect to the system: it sends DoH queries and TLS handshakes to the providers
 // and writes only its own history file. It never touches NRPT, DoH registrations, adapters or state.json.

 public sealed class BenchOptions {
  public int Attempts=6;            // rounds per profile; round i talks to servers[i % servers.Length]
  public int RequestTimeoutMs=2500; // per DoH request / handshake (working providers answer in 20-300 ms; a dead one costs this per server)
  public int TotalBudgetMs=30000;   // hard wall-clock limit for the whole run
  public int MaxParallel=6;         // profiles measured at the same time (each a different provider); each profile has at most one query and one handshake in flight
  public int AttemptGapMs=100;      // pause between rounds of one profile
 }

 public sealed class DomainResult {
  public string Host;public int Ok;public int Total;public bool Resolved;public bool TlsOk;public int Answers;public int Reachable;public string Error;
 }

 public sealed class ProfileResult {
  public string Id,Slot,Template;public string[] Servers;
  public int Attempts,Successes;public double Availability;
  public long MedianMs=-1,P95Ms=-1,TlsMs=-1;   // -1 = no sample
  public double Stability;public double Reach;
  public DomainResult[] Domains=new DomainResult[0];
  public bool DomainsOk,Alive,Selectable,Recommendable,Experimental,Best,Final,Truncated;
  public string Verdict="Pending";public int Score,Rank;public string Detail="";
 }

 public sealed class BenchRun {
  public string Id;public string StartedUtc;public long DurationMs;public int Attempts;public string CatalogVersion;
  public bool Cancelled,Truncated,Synthetic;public string Winner;public ProfileResult[] Results=new ProfileResult[0];
 }

 public sealed class BenchProgress {
  public string ProfileId;public int Done,Total;public ProfileResult Partial;public bool ProfileFinished;
 }

 public interface IBenchTransport {
  Task<TlsInfo> HandshakeAsync(ProfileSpec p,string server,int timeoutMs,CancellationToken ct);
  Task<Reply> QueryAsync(ProfileSpec p,string server,string host,int type,int timeoutMs,CancellationToken ct);
  Task<TlsInfo> AnswerTlsAsync(ProfileSpec p,string ip,string host,int timeoutMs,CancellationToken ct);
 }

 public static class Benchmark {
  public static string HistoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AntigravityDnsUnlocker","benchmark-history.json");
  public const int HistoryLimit=30;
  public const double ReachThreshold=0.75;
  static readonly object historyLock=new object();

  public static long Percentile(List<long> sorted,double p) {
   if(sorted.Count==0)return -1;
   int idx=(int)Math.Ceiling(p*sorted.Count)-1;return sorted[Math.Max(0,Math.Min(sorted.Count-1,idx))];
  }

  sealed class Acc {
   public ProfileSpec Spec;public int Attempts,Successes;public bool AnyQuery,Truncated,Cancelled;
   public readonly List<long> Warm=new List<long>(),Cold=new List<long>(),Tls=new List<long>();
   public readonly Dictionary<string,List<string>> Sets=new Dictionary<string,List<string>>();
   public readonly Dictionary<string,int> DomOk=new Dictionary<string,int>(),DomTotal=new Dictionary<string,int>();
   public readonly Dictionary<string,string> DomError=new Dictionary<string,string>();
   public DomainResult[] Domains=new DomainResult[0];public bool DomainsChecked;public int TargetCount;   // hosts of the scope this measurement started with
   public string LastError="";
  }

  static ProfileResult Snapshot(Acc a,bool final) {
   var r=new ProfileResult{Id=a.Spec.Id,Slot=a.Spec.Slot,Template=a.Spec.Template,Servers=a.Spec.Servers,Attempts=a.Attempts,Successes=a.Successes,Final=final,Truncated=a.Truncated,Experimental=a.Spec.Experimental};
   r.Availability=a.Attempts==0?0:Math.Round((double)a.Successes/a.Attempts,3);
   var lat=(a.Warm.Count>0?a.Warm:a.Cold).OrderBy(x=>x).ToList();
   r.MedianMs=Percentile(lat,0.5);r.P95Ms=Percentile(lat,0.95);
   var tls=a.Tls.OrderBy(x=>x).ToList();r.TlsMs=Percentile(tls,0.5);
   var fractions=new List<double>();
   foreach(var kv in a.Sets) {
    if(kv.Value.Count==0)continue;
    fractions.Add((double)kv.Value.GroupBy(x=>x).Max(g=>g.Count())/kv.Value.Count);
   }
   r.Stability=fractions.Count==0?0:Math.Round(fractions.Average(),3);
   r.Domains=a.Domains;
   int reachable=a.Domains.Sum(d=>d.Reachable),answers=a.Domains.Sum(d=>d.Answers);
   r.Reach=answers==0?0:Math.Round((double)reachable/answers,3);
   r.DomainsOk=a.DomainsChecked&&a.Domains.Length==a.TargetCount&&a.Domains.All(d=>d.Resolved&&d.TlsOk);
   r.Alive=a.AnyQuery;
   if(!final){r.Verdict="Pending";return r;}
   if(a.Attempts==0){r.Verdict="NotMeasured";r.Detail=a.Cancelled?"cancelled":"time budget exhausted";return r;}
   double blended=0.6*(r.MedianMs<0?5000:r.MedianMs)+0.4*(r.P95Ms<0?5000:r.P95Ms);
   double latScore=Math.Max(0,Math.Min(1,1-(blended-100)/1400));
   double score=55*r.Availability+20*r.Stability+25*latScore;
   if(!r.Alive)r.Verdict="Dead";
   else if(!r.DomainsOk)r.Verdict="Partial";
   else if(r.Availability<0.8||r.Stability<0.7||r.Reach<ReachThreshold)r.Verdict="Flaky";
   else if(r.MedianMs>700)r.Verdict="Slow";
   else r.Verdict="Good";
   if(!r.DomainsOk)score*=0.4;
   if(!r.Alive)score=0;
   r.Score=(int)Math.Round(score);
   // a pool with dead answer addresses is not fit for use. The reach threshold is the one behind the "Flaky" verdict (75 %).
   r.Selectable=r.Alive&&r.DomainsOk&&r.Availability>=0.6&&r.Stability>=0.6&&r.Reach>=ReachThreshold;
   // An experimental profile is never recommended until it earns the "Good" verdict; it stays selectable by hand.
   r.Recommendable=r.Selectable&&(!r.Experimental||r.Verdict=="Good");
   var notes=new List<string>();
   foreach(var d in r.Domains)if(!d.Resolved||!d.TlsOk)notes.Add(d.Host.Split('.')[0]+": "+(d.Error??(d.Resolved?"TLS":"no answer")));
   if(r.Reach>0&&r.Reach<1)notes.Add("reach "+(int)Math.Round(r.Reach*100)+"%");
   if(!string.IsNullOrEmpty(a.LastError)&&!a.LastError.StartsWith("SKIPPED")&&r.Availability<1&&!notes.Any(n=>n.Contains(a.LastError)))notes.Add("last error: "+a.LastError);
   r.Detail=string.Join("; ",notes);
   return r;
  }

  static void Rank(ProfileResult[] results) {
   var ordered=results.OrderByDescending(r=>r.Recommendable).ThenByDescending(r=>r.Selectable).ThenByDescending(r=>r.Score).ThenBy(r=>r.MedianMs<0?long.MaxValue:r.MedianMs).ThenBy(r=>r.Id,StringComparer.OrdinalIgnoreCase).ToList();
   for(int i=0;i<ordered.Count;i++){ordered[i].Rank=i+1;ordered[i].Best=false;}
   if(ordered.Count>0&&ordered[0].Recommendable)ordered[0].Best=true;
  }

  public static async Task<BenchRun> RunAsync(IEnumerable<ProfileSpec> profiles,BenchOptions o,IBenchTransport transport,IProgress<BenchProgress> progress=null,CancellationToken ct=default,bool synthetic=false) {
   var specs=profiles.ToList();var sw=Stopwatch.StartNew();
   var run=new BenchRun{Id=Guid.NewGuid().ToString("N"),StartedUtc=DateTime.UtcNow.ToString("o"),Attempts=o.Attempts,CatalogVersion=ProfileCatalog.BuiltIn.Version,Synthetic=synthetic};
   var accs=specs.Select(s=>new Acc{Spec=s}).ToArray();
   using(var budget=CancellationTokenSource.CreateLinkedTokenSource(ct)) {
    budget.CancelAfter(o.TotalBudgetMs);
    int finished=0;var gate=new SemaphoreSlim(Math.Max(1,o.MaxParallel));
    var tasks=accs.Select(a=>Task.Run(async()=>{
     try{await gate.WaitAsync(budget.Token);}catch(OperationCanceledException){a.Cancelled=ct.IsCancellationRequested;return;}
     try{await Measure(a,o,transport,budget.Token,ct,progress);}
     finally{gate.Release();}
     int done=Interlocked.Increment(ref finished);
     progress?.Report(new BenchProgress{ProfileId=a.Spec.Id,Done=done,Total=specs.Count,Partial=Snapshot(a,true),ProfileFinished=true});
    })).ToArray();
    await Task.WhenAll(tasks);
    run.Cancelled=ct.IsCancellationRequested;
    run.Truncated=!run.Cancelled&&budget.IsCancellationRequested;
   }
   run.Results=accs.Select(a=>Snapshot(a,true)).ToArray();
   Rank(run.Results);
   run.Winner=run.Results.FirstOrDefault(r=>r.Best)?.Id;
   run.DurationMs=sw.ElapsedMilliseconds;
   return run;
  }

  static async Task Measure(Acc a,BenchOptions o,IBenchTransport t,CancellationToken budget,CancellationToken user,IProgress<BenchProgress> progress) {
   var p=a.Spec;var targets=Program.Hosts;a.TargetCount=targets.Length;var warmedServers=new HashSet<string>();
   foreach(string d in targets){a.Sets[d]=new List<string>();a.DomOk[d]=0;a.DomTotal[d]=0;}
   try {
    for(int i=0;i<o.Attempts;i++) {
     budget.ThrowIfCancellationRequested();
     string server=p.Servers[i%p.Servers.Length];
     // The TLS-time probe runs beside the first query (separate connection); it is skipped nowhere, but a profile whose endpoint
     // refuses both is not asked a second question in the same round, so a dead provider costs one timeout per round, not three.
     var hsTask=t.HandshakeAsync(p,server,o.RequestTimeoutMs,budget);
     bool okAttempt=true,endpointDown=false;
     for(int h=0;h<targets.Length;h++) {
      string host=targets[h];
      Reply r;
      if(h>0&&endpointDown)r=new Reply{Code="SKIPPED_ENDPOINT_DOWN",Addresses=new string[0]};
      else r=await t.QueryAsync(p,server,host,1,o.RequestTimeoutMs,budget);
      bool cold=h==0&&warmedServers.Add(server); // first query on a server pays connection setup; it counts for availability, not for latency
      bool ok=r.Code=="DNS_OK"&&r.Addresses!=null&&r.Addresses.Length>0&&r.Addresses.All(Program.PublicIp);
      a.DomTotal[host]++;
      if(ok){a.AnyQuery=true;a.DomOk[host]++;(cold?a.Cold:a.Warm).Add(r.Milliseconds);a.Sets[host].Add(string.Join(",",r.Addresses.OrderBy(x=>x,StringComparer.Ordinal)));}
      else{okAttempt=false;a.LastError=r.Code??"ERR";a.DomError[host]=r.Code??"ERR";}
      if(h==0&&!ok){var early=await hsTask;endpointDown=!early.Ok;}
     }
     var hs=await hsTask;
     if(hs.Ok)a.Tls.Add(hs.ConnectMs+hs.HandshakeMs);else if(string.IsNullOrEmpty(a.LastError))a.LastError=hs.Error??"TLS";
     a.Attempts++;if(okAttempt)a.Successes++;
     progress?.Report(new BenchProgress{ProfileId=p.Id,Done=a.Attempts,Total=o.Attempts,Partial=Snapshot(a,false)});
     // Circuit breaker: every server tried once and nothing ever answered -> the provider is dead; stop spending requests on it.
     if(!a.AnyQuery&&a.Tls.Count==0&&a.Attempts>=Math.Max(1,Math.Min(p.Servers.Length,3)))break;
     if(i<o.Attempts-1&&o.AttemptGapMs>0)await Task.Delay(o.AttemptGapMs,budget);
    }
   }
   catch(OperationCanceledException){a.Truncated=true;a.Cancelled=user.IsCancellationRequested;}
   // Google-domain check: does the provider's answer lead to a TLS-valid host? Not repeated per attempt (no flood), shared across profiles by the transport.
   var domains=new List<DomainResult>();
   foreach(string host in targets) {
    var dr=new DomainResult{Host=host,Ok=a.DomOk[host],Total=a.DomTotal[host],Resolved=a.DomOk[host]>0};
    if(dr.Resolved&&!budget.IsCancellationRequested) {
     var modal=a.Sets[host].GroupBy(x=>x).OrderByDescending(g=>g.Count()).First().Key.Split(',');
     var ips=modal.Take(4).ToArray();dr.Answers=ips.Length;
     try {
      var checks=await Task.WhenAll(ips.Select(ip=>t.AnswerTlsAsync(p,ip,host,o.RequestTimeoutMs,budget)));
      dr.Reachable=checks.Count(c=>c.Ok);dr.TlsOk=dr.Reachable>0;
      if(!dr.TlsOk)dr.Error=checks.Select(c=>c.Error).FirstOrDefault(e=>!string.IsNullOrEmpty(e))??"TLS";
     }catch(OperationCanceledException){dr.Error="NOT_CHECKED";a.Truncated=true;}
    }
    else if(dr.Resolved)dr.Error="NOT_CHECKED";
    else dr.Error=a.DomError.TryGetValue(host,out var e)?e:"NO_ANSWER";
    domains.Add(dr);
   }
   a.Domains=domains.ToArray();a.DomainsChecked=true;
  }

  // ---- history ----
  public static List<BenchRun> LoadHistory() {
   lock(historyLock) {
    try {
     if(!File.Exists(HistoryPath))return new List<BenchRun>();
     using(var doc=JsonDocument.Parse(File.ReadAllText(HistoryPath))) {
      if(!doc.RootElement.TryGetProperty("Runs",out var runs))return new List<BenchRun>();
      return JsonSerializer.Deserialize<List<BenchRun>>(runs.GetRawText(),Program.JsonOptions)??new List<BenchRun>();
     }
    }catch{return new List<BenchRun>();}
   }
  }
  public static bool AppendHistory(BenchRun run) {
   lock(historyLock) {
    try {
     var runs=LoadHistory();runs.Add(run);if(runs.Count>HistoryLimit)runs=runs.Skip(runs.Count-HistoryLimit).ToList();
     Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath));
     string tmp=HistoryPath+"."+Guid.NewGuid().ToString("N")+".tmp";
     File.WriteAllText(tmp,JsonSerializer.Serialize(new{Schema=1,Runs=runs},Program.JsonOptions),new UTF8Encoding(false));
     File.Move(tmp,HistoryPath,true);return true;
    }catch{return false;}
   }
  }
 }

 // Real network: DoH over HTTP/2 to the pinned IP (port taken from the template), TLS validated by name.
 public sealed class LiveTransport:IBenchTransport,IDisposable {
  readonly ConcurrentDictionary<string,HttpClient> clients=new ConcurrentDictionary<string,HttpClient>();
  readonly ConcurrentDictionary<string,Task<TlsInfo>> answerCache=new ConcurrentDictionary<string,Task<TlsInfo>>();
  HttpClient Client(ProfileSpec p,string server)=>clients.GetOrAdd(p.Id+"|"+server,_=>{
   var ip=IPAddress.Parse(server);
   var handler=new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false,PooledConnectionIdleTimeout=TimeSpan.FromSeconds(30)};
   handler.SslOptions.RemoteCertificateValidationCallback=CertRevocation.Callback;
   handler.ConnectCallback=async(ctx,ct)=>{var s=new Socket(ip.AddressFamily,SocketType.Stream,ProtocolType.Tcp){NoDelay=true};try{await s.ConnectAsync(ip,ctx.DnsEndPoint.Port,ct);return new NetworkStream(s,true);}catch{s.Dispose();throw;}};
   return new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
  });
  public Task<TlsInfo> HandshakeAsync(ProfileSpec p,string server,int timeoutMs,CancellationToken ct)=>Probing.TlsAsync(server,p.Port,p.Host,timeoutMs,ct);
  public Task<Reply> QueryAsync(ProfileSpec p,string server,string host,int type,int timeoutMs,CancellationToken ct)=>Wire.DohAsync(Client(p,server),p.Template,host,type,timeoutMs,ct);
  public Task<TlsInfo> AnswerTlsAsync(ProfileSpec p,string ip,string host,int timeoutMs,CancellationToken ct)=>answerCache.GetOrAdd(ip+"|"+host,_=>Probing.TlsAsync(ip,443,host,timeoutMs,CancellationToken.None));
  public void Dispose(){foreach(var c in clients.Values)c.Dispose();clients.Clear();}
 }

 // Scripted transport for offline tests: replies are real DNS wire packets (built by tests\helpers\SyntheticWire.ps1) parsed by the real Wire.Parse.
 public sealed class SyntheticTransport:IBenchTransport {
  sealed class Step{public bool Ok=true;public int Ms;public bool Timeout;public string Packet;public int Delay;}
  sealed class Script{public Step[] Tls=new Step[0];public Dictionary<string,Step[]> Queries=new Dictionary<string,Step[]>();public Dictionary<string,bool> AnswerTls=new Dictionary<string,bool>();public int Tick;public readonly Dictionary<string,int> Q=new Dictionary<string,int>();}
  readonly Dictionary<string,Script> scripts=new Dictionary<string,Script>();
  public readonly List<ProfileSpec> Profiles=new List<ProfileSpec>();
  public BenchOptions Options=new BenchOptions();
  public int Requests;
  public static SyntheticTransport Load(string json) {
   var t=new SyntheticTransport();
   using(var doc=JsonDocument.Parse(json)) {
    var root=doc.RootElement;
    if(root.TryGetProperty("attempts",out var at))t.Options.Attempts=at.GetInt32();
    if(root.TryGetProperty("totalBudgetMs",out var tb))t.Options.TotalBudgetMs=tb.GetInt32();
    if(root.TryGetProperty("requestTimeoutMs",out var rt))t.Options.RequestTimeoutMs=rt.GetInt32();
    t.Options.AttemptGapMs=0;
    foreach(var pe in root.GetProperty("profiles").EnumerateArray()) {
     var spec=new ProfileSpec{Experimental=pe.TryGetProperty("experimental",out var exp)&&exp.GetBoolean(),Id=pe.GetProperty("id").GetString(),Slot=pe.TryGetProperty("slot",out var sl)?sl.GetString():null,Template=pe.GetProperty("template").GetString(),Servers=pe.GetProperty("servers").EnumerateArray().Select(x=>x.GetString()).ToArray()};
     var sc=new Script();
     if(pe.TryGetProperty("tls",out var tls))sc.Tls=tls.EnumerateArray().Select(ReadStep).ToArray();
     if(pe.TryGetProperty("queries",out var qs))foreach(var q in qs.EnumerateObject())sc.Queries[q.Name]=q.Value.EnumerateArray().Select(ReadStep).ToArray();
     if(pe.TryGetProperty("answerTls",out var an))foreach(var x in an.EnumerateObject())sc.AnswerTls[x.Name]=x.Value.GetBoolean();
     t.scripts[spec.Id]=sc;t.Profiles.Add(spec);
    }
   }
   return t;
  }
  static Step ReadStep(JsonElement e)=>new Step{
   Ok=!e.TryGetProperty("ok",out var ok)||ok.GetBoolean(),Ms=e.TryGetProperty("ms",out var ms)?ms.GetInt32():10,
   Timeout=e.TryGetProperty("timeout",out var to)&&to.GetBoolean(),Packet=e.TryGetProperty("packet",out var pk)?pk.GetString():null,Delay=e.TryGetProperty("delay",out var dl)?dl.GetInt32():0};
  public async Task<TlsInfo> HandshakeAsync(ProfileSpec p,string server,int timeoutMs,CancellationToken ct) {
   Interlocked.Increment(ref Requests);var sc=scripts[p.Id];
   int i;lock(sc){i=sc.Tick++;}
   var step=sc.Tls.Length==0?new Step():sc.Tls[i%sc.Tls.Length];
   if(step.Delay>0)await Task.Delay(step.Delay,ct);
   return step.Ok?new TlsInfo{Ok=true,ConnectMs=step.Ms/4,HandshakeMs=step.Ms-step.Ms/4}:new TlsInfo{Ok=false,Error="SYNTHETIC_TLS_FAILURE"};
  }
  public async Task<Reply> QueryAsync(ProfileSpec p,string server,string host,int type,int timeoutMs,CancellationToken ct) {
   Interlocked.Increment(ref Requests);var sc=scripts[p.Id];
   if(!sc.Queries.TryGetValue(host,out var steps)||steps.Length==0)return new Reply{Code="DNS_TIMEOUT_OR_UNREACHABLE",Addresses=new string[0],Cnames=new string[0],Milliseconds=timeoutMs};
   int i;lock(sc){sc.Q.TryGetValue(host,out i);sc.Q[host]=i+1;}
   var step=steps[i%steps.Length];
   if(step.Delay>0)await Task.Delay(step.Delay,ct);
   ct.ThrowIfCancellationRequested();
   Reply r;
   if(step.Timeout||step.Packet==null)r=new Reply{Code="DNS_TIMEOUT_OR_UNREACHABLE"};
   else {
    var bytes=Convert.FromBase64String(step.Packet);int id=(bytes[0]<<8)|bytes[1];
    try{r=Wire.Parse(bytes,host,type,id);}catch(FormatException){r=new Reply{Code="DNS_INVALID_RESPONSE"};}
   }
   r.Addresses=r.Addresses??new string[0];r.Cnames=r.Cnames??new string[0];r.Host=host;r.Type=type;r.Server=server;r.Transport="SYNTHETIC";
   r.Milliseconds=step.Timeout?timeoutMs:step.Ms;return r;
  }
  public Task<TlsInfo> AnswerTlsAsync(ProfileSpec p,string ip,string host,int timeoutMs,CancellationToken ct) {
   var sc=scripts[p.Id];
   if(sc.AnswerTls.TryGetValue(host+"|"+ip,out var v)||sc.AnswerTls.TryGetValue(host,out v)||sc.AnswerTls.TryGetValue(ip,out v))return Task.FromResult(v?new TlsInfo{Ok=true}:new TlsInfo{Ok=false,Error="SYNTHETIC_ANSWER_TLS_FAILURE"});
   return Task.FromResult(new TlsInfo{Ok=true});
  }
 }
}
