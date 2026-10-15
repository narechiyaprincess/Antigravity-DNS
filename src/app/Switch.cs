using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgDns {
 // the current provider has no usable address. The program looks for another catalog profile that works on this network and OFFERS it.
 // Nothing is switched silently: the offer becomes a connection only through the trust dialog and the ordinary enable (which handles a running bridge
 // as a compensated profile switch). A bridge that exists is never removed by the search.
 public sealed class SwitchOffer {
  public string ProfileId;   // the profile that passed the check
  public string FromId;      // the profile that had no usable address
  public string Reason;      // the code that triggered the search
 }

 public enum SwitchKind { Offer, NoWorking, Cancelled }
 public sealed class SwitchOutcome {
  public SwitchKind Kind;public SwitchOffer Offer;public ProfileResult Result;public int Checked;
 }

 public static class ProviderSwitch {
  // The failures that mean "this provider has no address that works right now". Anything else (a conflict, a missing right, a dead WMI) is not a reason to change provider.
  public static readonly string[] Triggers={"BOOTSTRAP_NO_ADDRESS","BOOTSTRAP_TIMEOUT","DOH_NO_VALIDATED_ENDPOINT"};
  public static bool IsTrigger(string code)=>Triggers.Contains(CodeTable.TokenOf(code));

  // What is measured: the non-experimental profiles of the catalog, each with its pins and the addresses an earlier bootstrap found for it (not older than 30 days).
  public static List<ProfileSpec> Pool(ProfileCatalog catalog,List<SavedAddr> saved,DateTime nowUtc) {
   var pool=new List<ProfileSpec>();
   foreach(var p in catalog.Profiles.Where(x=>!x.Experimental)) {
    var servers=p.Servers.Concat(SavedAddresses.Usable(SavedAddresses.Find(saved,p.Template),nowUtc)).Distinct().ToArray();
    pool.Add(new ProfileSpec{Id=p.Id,Slot=p.Slot,Template=p.Template,Servers=servers,Experimental=false,Origin=p.Origin});
   }
   return pool;
  }

  // The benchmark's own idea of "works": Selectable (alive, both domains, availability, stability and at least 75 % of the answer addresses reachable).
  // An experimental profile is never offered, even when it measures well; the profile that just failed is not offered back.
  public static ProfileResult Pick(BenchRun run,string currentId)=>run.Results
   .Where(r=>r.Selectable&&!r.Experimental&&!string.Equals(r.Id,currentId,StringComparison.OrdinalIgnoreCase))
   .OrderBy(r=>r.Rank==0?int.MaxValue:r.Rank).FirstOrDefault();

  // Read-only: DoH queries and TLS handshakes, like the benchmark tab. No state, no NRPT, no elevation.
  public static async Task<SwitchOutcome> FindAsync(IEnumerable<ProfileSpec> pool,string currentId,IBenchTransport transport,BenchOptions options,CancellationToken ct) {
   var candidates=pool.Where(p=>!p.Experimental&&p.Servers!=null&&p.Servers.Length>0&&!string.Equals(p.Id,currentId,StringComparison.OrdinalIgnoreCase)).ToList();
   var outcome=new SwitchOutcome{Kind=SwitchKind.NoWorking,Checked=candidates.Count};
   if(candidates.Count==0)return outcome;
   var run=await Benchmark.RunAsync(candidates,options,transport,null,ct);
   if(run.Cancelled||ct.IsCancellationRequested){outcome.Kind=SwitchKind.Cancelled;return outcome;}
   var best=Pick(run,currentId);
   if(best==null)return outcome;
   outcome.Kind=SwitchKind.Offer;outcome.Result=best;outcome.Offer=new SwitchOffer{ProfileId=best.Id,FromId=currentId};
   return outcome;
  }

  // The only way an offer is carried out: first the person's trust decision for that provider, then the ordinary enable. A refusal applies nothing.
  public static async Task<bool> ApplyAsync(SwitchOffer offer,Func<string,bool> trust,Func<string,Task> enable) {
   if(offer==null||string.IsNullOrEmpty(offer.ProfileId))return false;
   if(!trust(offer.ProfileId))return false;
   await enable(offer.ProfileId);
   return true;
  }
 }
}
