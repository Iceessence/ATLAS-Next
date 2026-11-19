using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using AtlasNext;

internal static class ReferenceChecks
{
    private const string Name="local:fixture";
    private static readonly string Digest=new('b',64);
    private sealed class Fixture(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)=>send(request,cancel);
    }

    public static async Task Run(Action<bool,string> check)
    {
        int turns=0;
        using var client=new LocalReference(new Fixture(async (request,token)=>{
            if(request.RequestUri!=new Uri(LocalReference.Address+(request.Method==HttpMethod.Get ? "api/tags" : "api/chat")))
                throw new InvalidOperationException("unexpected endpoint");
            if(request.Method==HttpMethod.Get)
                return new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(new {models=new[]{
                    new {name=Name,digest=Digest,size=10,details=new {format="gguf"}},
                    new {name="request:cloud",digest=Digest,size=10,details=new {format="gguf"}},
                    new {name="unknown.example/remote:tag",digest=Digest,size=10,details=new {format="gguf"}}
                }}))};
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            int count=json.RootElement.GetProperty("messages").GetArrayLength();
            if(count!=++turns*2-1) throw new InvalidOperationException("missing conversation history");
            return new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(new {model=Name,done=true,message=new {role="assistant",content=$"reply-{turns}"}}))};
        }));
        var model=await client.Discover(CancellationToken.None);
        check(model is [{Name:Name}],"local chat discovery filters namespace and cloud model identifiers");
        string reply=await client.Reply(model[0],[new("user","First turn")],CancellationToken.None);
        string follow=await client.Reply(model[0],[new("user","First turn"),new("assistant",reply),new("user","Follow up")],CancellationToken.None);
        check(reply=="reply-1" && follow=="reply-2","loopback reference chat sends only bounded ordered conversation turns");
        using var redirect=new LocalReference(new Fixture((_,_)=>Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Redirect) {Headers={Location=new Uri("https://example.invalid/")}})));
        try { await redirect.Discover(CancellationToken.None); check(false,"redirect refused"); }
        catch(HttpRequestException) { check(true,"local client refuses redirection response"); }
        using var oversized=new LocalReference(new Fixture((_,_)=>Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(new string('x',300_000))})));
        try { await oversized.Discover(CancellationToken.None); check(false,"oversized response refused"); }
        catch(InvalidDataException) { check(true,"local client bounds response consumption"); }
        using var cancellation=new CancellationTokenSource();
        using var waiting=new LocalReference(new Fixture(async (_,token)=>{
            await Task.Delay(Timeout.InfiniteTimeSpan,token);
            return new(HttpStatusCode.OK);
        }));
        var pending=waiting.Discover(cancellation.Token);
        cancellation.Cancel();
        try { await pending; check(false,"chat cancellation observed"); }
        catch(OperationCanceledException) { check(true,"loopback request observes owner cancellation"); }
    }

    public static async Task<int> Smoke(string id)
    {
        using var client=new LocalReference();
        var model=(await client.Discover(CancellationToken.None)).Single(m=>m.Name==id);
        var watch=Stopwatch.StartNew();
        var first=new ReferenceTurn("user","Reply with exactly BRIDGE and no punctuation.");
        string answer=await client.Reply(model,[first],CancellationToken.None);
        double firstMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var second=new ReferenceTurn("user","Append -LOCAL to your previous reply. Reply with only the resulting string.");
        string follow=await client.Reply(model,[first,new("assistant",answer),second],CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new {model,answer,follow,firstMs,secondMs=watch.Elapsed.TotalMilliseconds,
            workingSetBytes=Environment.WorkingSet,assistance="external reference"},Assets.Json));
        return answer.Trim()=="BRIDGE" && follow.Trim()=="BRIDGE-LOCAL" ? 0 : 1;
    }
}
