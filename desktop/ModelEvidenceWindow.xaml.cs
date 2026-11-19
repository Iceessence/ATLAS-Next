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
        return string.Join("\n",lines);
    }
}
