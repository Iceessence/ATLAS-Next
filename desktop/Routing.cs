using System.IO;
using System.Text.Json.Serialization;

namespace AtlasNext;

internal record Weights(double[][] W,double[] B,double[][] V,double[] C);
internal record Manifest(string Id,string Origin,string Qualification,string[] Routes,
    [property: JsonPropertyName("checkpoint_sha256")] string Digest);
internal record Recommendation(string Model,string Origin,string Qualification,string Sha256,string Route,double Probability);

internal sealed class Routing
{
    private readonly Weights weights;
    private readonly Manifest manifest;
    public Routing()
    {
        (weights,var hash) = Assets.Read<Weights>("native.json");
        manifest = Assets.Read<Manifest>("native.manifest.json").value;
        if (hash != manifest.Digest || manifest.Qualification != "shadow" || manifest.Origin != "original" ||
            !manifest.Routes.SequenceEqual(new[]{"fact","procedure","clarify"}) ||
            weights.W.Length!=256 || weights.W.Any(x=>x.Length!=16) || weights.B.Length!=16 ||
            weights.V.Length!=16 || weights.V.Any(x=>x.Length!=3) || weights.C.Length!=3)
            throw new InvalidDataException("Invalid original policy release contract");
    }

    public Recommendation Infer(string text)
    {
        var indices = new HashSet<int>();
        foreach (var word in Assets.Tokens(text))
        {
            uint hash = 2166136261;
            foreach (var c in word) hash = unchecked((hash ^ c) * 16777619);
            indices.Add((int)(hash % 256));
        }
        var logits = weights.C.ToArray();
        double norm = Math.Sqrt(Math.Max(1, indices.Count));
        for (int j=0;j<weights.B.Length;j++)
        {
            double h = Math.Tanh(weights.B[j]+indices.Sum(i=>weights.W[i][j])/norm);
            for(int k=0;k<logits.Length;k++) logits[k]+=h*weights.V[j][k];
        }
        double max = logits.Max();
        int index = Array.IndexOf(logits,max);
        return new(manifest.Id,manifest.Origin,manifest.Qualification,manifest.Digest,
            manifest.Routes[index],1/logits.Sum(x=>Math.Exp(x-max)));
    }

    public static string Rule(string text)
    {
        var words=Assets.Tokens(text).ToHashSet();
        if(words.Overlaps(["how","steps","procedure","restore","rebuild","guide","instructions","recover","walkthrough","configure"]))
            return "procedure";
        if(words.Overlaps(["what","which","limit","policy","duration","where","size","version","when","retention","default"]))
            return "fact";
        return "clarify";
    }
}
