using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AgDns {
 // The ping shown on the main page while the bridge works: the round trip of a TCP connect to the provider's intermediary server for an Antigravity host.
 // Read-only, like the benchmark: one DoH query to the active provider for the proxy address, then a few TLS handshakes with full certificate
 // validation (SNI = the Google host). A sample counts only if TLS passed, so the number always belongs to a proxy that really serves the host.
 public interface ISpeedProbe {
  Task<Reply> Doh(string template,string server,string host,int timeoutMs,CancellationToken ct);
  Task<TlsInfo> Tls(string ip,string host,int timeoutMs,CancellationToken ct);
 }

 public sealed class SpeedResult {
  public long Ms=-1;          // median TCP connect time to the proxy; -1 = no valid sample
  public string Proxy;        // the address that was measured
  public string Error;        // NO_BRIDGE, NO_ANSWER (provider gave no public IPv4), TLS_FAILED
  public DateTime Utc;
 }

 public static class BridgeSpeed {
  public const int Samples=3,TimeoutMs=2500;
  public static readonly TimeSpan Fresh=TimeSpan.FromMinutes(2);   // a newer result is shown as is; an older one is measured again on the next look

  public static async Task<SpeedResult> MeasureAsync(ISpeedProbe probe,string template,string[] servers,string host,CancellationToken ct) {
   var res=new SpeedResult{Utc=DateTime.UtcNow};
   if(string.IsNullOrWhiteSpace(template)||servers==null||servers.Length==0||string.IsNullOrWhiteSpace(host)){res.Error="NO_BRIDGE";return res;}
   foreach(string s in servers) {
    ct.ThrowIfCancellationRequested();
    Reply r;try{r=await probe.Doh(template,s,host,TimeoutMs,ct);}catch(OperationCanceledException){throw;}catch{continue;}
    res.Proxy=r?.Addresses?.FirstOrDefault(a=>IPAddress.TryParse(a,out var ip)&&ip.AddressFamily==AddressFamily.InterNetwork&&Program.PublicIp(a));
    if(res.Proxy!=null)break;
   }
   if(res.Proxy==null){res.Error="NO_ANSWER";return res;}
   var times=new List<long>();
   for(int i=0;i<Samples;i++) {
    ct.ThrowIfCancellationRequested();
    TlsInfo t;try{t=await probe.Tls(res.Proxy,host,TimeoutMs,ct);}catch(OperationCanceledException){throw;}catch{t=null;}
    if(t!=null&&t.Ok)times.Add(t.ConnectMs);
   }
   ct.ThrowIfCancellationRequested();
   if(times.Count==0){res.Error="TLS_FAILED";return res;}
   times.Sort();res.Ms=times[times.Count/2];res.Utc=DateTime.UtcNow;return res;
  }

  // Plain words for the ping row: up to 80 ms is fast, up to 200 ms is normal for a provider server abroad, more is slow.
  public static string Grade(long ms)=>ms<0?"":L.T(ms<=80?"быстро":ms<=200?"нормально":"медленно");
  public static string ValueText(SpeedResult r)=>r==null?L.T("измеряю…"):r.Ms>=0?r.Ms+L.T(" мс")+" · "+Grade(r.Ms):L.T("не ответил");

  public sealed class Live:ISpeedProbe,IDisposable {
   readonly LiveTransport t=new LiveTransport();
   public Task<Reply> Doh(string template,string server,string host,int timeoutMs,CancellationToken ct)=>t.QueryAsync(new ProfileSpec{Id="speed",Template=template},server,host,1,timeoutMs,ct);
   public Task<TlsInfo> Tls(string ip,string host,int timeoutMs,CancellationToken ct)=>Probing.TlsAsync(ip,443,host,timeoutMs,ct);
   public void Dispose()=>t.Dispose();
  }
 }
}
