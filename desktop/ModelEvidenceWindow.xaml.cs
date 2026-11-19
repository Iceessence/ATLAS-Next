using System.Text.Json;
using System.Windows;

namespace AtlasNext;

public partial class ModelEvidenceWindow : Window
{
    internal ModelEvidenceWindow()
    {
        InitializeComponent();
        ModelReport.Text=ReadReport();
    }

    internal static string ReadReport()
    {
        var (router,_)=Assets.Read<JsonElement>("native.manifest.json");
        var (report,hash)=Assets.Read<JsonElement>("experiments/next-014.json");
        var lines=new List<string> {
            "ATLAS-original desktop router · "+router.GetProperty("qualification").GetString(),
            router.GetProperty("id").GetString() ?? "",
            "Checkpoint SHA-256 "+router.GetProperty("checkpoint_sha256").GetString(),
            "",
            "ATLAS-original project specialist · "+report.GetProperty("qualification").GetString(),
            "Checkpoint SHA-256 "+report.GetProperty("checkpointSha256").GetString(),
            "",
            "NEXT-014 · fitting "+report.GetProperty("fitStatus").GetString(),
            report.GetProperty("fitReason").GetString() ?? "",
            report.GetProperty("observedWeakness").GetString() ?? "",
            "New eligible training rows "+report.GetProperty("newlyEligibleTrainingRows").GetInt32(),
            ""
        };
        foreach(var policy in report.GetProperty("metrics").EnumerateObject()) {
            var values=policy.Value;
            lines.Add($"{policy.Name} · {values.GetProperty("useful").GetInt32()}/{values.GetProperty("total").GetInt32()} useful results · "+
                $"{values.GetProperty("wrong_deliveries").GetInt32()} wrong deliveries · {values.GetProperty("host_withheld").GetInt32()} withholds");
        }
        lines.Add($"\nReceipt SHA-256 {hash}\n\nLocal chat uses the owner-selected external reference model. Chat replies are unverified.");
        var (native,nativeHash)=Assets.Read<JsonElement>("experiments/next-020.json");
        lines.Add("");
        lines.Add("Native-original lab · "+native.GetProperty("qualification").GetString());
        lines.Add($"External inference calls {native.GetProperty("externalInferenceCalls").GetInt32()}");
        lines.Add(native.GetProperty("provenance").GetString() ?? "");
        foreach(var model in native.GetProperty("models").EnumerateArray()) {
            lines.Add($"\n{model.GetProperty("role").GetString()} · {model.GetProperty("architecture").GetString()} · "+
                $"{model.GetProperty("parameters").GetInt32()} parameters");
            lines.Add($"{model.GetProperty("trainingExamples").GetInt32()} training examples · "+
                $"{model.GetProperty("positives").GetInt32()} verified positives");
            lines.Add("Checkpoint SHA-256 "+model.GetProperty("checkpointSha256").GetString());
        }
        var results=native.GetProperty("publicEvaluation");
        int total=results.GetProperty("tasks").GetInt32();
        lines.Add($"\nPublic held-out tasks · host {results.GetProperty("host").GetInt32()}/{total} · "+
            $"TF-IDF {results.GetProperty("tfidf").GetInt32()}/{total} · linear {results.GetProperty("linear").GetInt32()}/{total} · "+
            $"specialist {results.GetProperty("specialist").GetInt32()}/{total} · cooperation {results.GetProperty("cooperation").GetInt32()}/{total}");
        var refit=native.GetProperty("refit");
        lines.Add($"Refit {refit.GetProperty("disposition").GetString()} · {refit.GetProperty("useful").GetInt32()}/{total}");
        lines.Add(refit.GetProperty("reason").GetString() ?? "");
        lines.Add(native.GetProperty("scoring").GetString() ?? "");
        lines.Add(native.GetProperty("runHint").GetString() ?? "");
        lines.Add($"Receipt SHA-256 {nativeHash}");
        return string.Join("\n",lines);
    }
}
