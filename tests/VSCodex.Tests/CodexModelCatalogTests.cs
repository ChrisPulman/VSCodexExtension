// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using TUnit.Assertions.Enums;
using VSCodex.Core.Models;

namespace VSCodex.Tests;

/// <summary>Verifies the current Codex application model and effort catalog.</summary>
public sealed class CodexModelCatalogTests
{
    /// <summary>Stores the GPT-5.3 Codex Spark model identifier.</summary>
    private const string Gpt53CodexSpark = "gpt-5.3-codex-spark";

    /// <summary>Stores the GPT-5.4 model identifier.</summary>
    private const string Gpt54 = "gpt-5.4";

    /// <summary>Stores the GPT-5.4 Mini model identifier.</summary>
    private const string Gpt54Mini = "gpt-5.4-mini";

    /// <summary>Stores the GPT-5.5 model identifier.</summary>
    private const string Gpt55 = "gpt-5.5";

    /// <summary>Stores the GPT-5.6 Luna model identifier.</summary>
    private const string Gpt56Luna = "gpt-5.6-luna";

    /// <summary>Stores the GPT-5.6 Sol model identifier.</summary>
    private const string Gpt56Sol = "gpt-5.6-sol";

    /// <summary>Stores the GPT-5.6 Terra model identifier.</summary>
    private const string Gpt56Terra = "gpt-5.6-terra";

    /// <summary>Stores the medium effort identifier.</summary>
    private const string MediumEffort = "medium";

    /// <summary>Stores the ultra effort identifier.</summary>
    private const string UltraEffort = "ultra";

    /// <summary>Stores the extra-high effort identifier.</summary>
    private const string XhighEffort = "xhigh";

    /// <summary>Verifies the current model picker order and Power default.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Current_models_match_the_Codex_application_catalog()
    {
        await Assert.That(CodexModelCatalog.DefaultModel).IsEqualTo(Gpt56Sol);
        await Assert.That(CodexModelCatalog.DefaultFailoverModel).IsEqualTo(Gpt56Terra);
        await Assert.That(CodexModelCatalog.DefaultBudgetModel).IsEqualTo(Gpt56Luna);
        await Assert.That(CodexModelCatalog.DefaultReasoningEffort).IsEqualTo(MediumEffort);
        await Assert.That(CodexModelCatalog.SupportedModels).IsEquivalentTo(
            [
                Gpt56Sol,
                Gpt56Terra,
                Gpt56Luna,
                Gpt55,
                Gpt54,
                Gpt54Mini,
                Gpt53CodexSpark
            ],
            CollectionOrdering.Matching);
    }

    /// <summary>Verifies that Sol, Terra, and Luna expose their current effort limits.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Gpt56_efforts_are_model_aware()
    {
        await Assert.That(CodexModelCatalog.GetReasoningEfforts(Gpt56Sol))
            .IsEquivalentTo(["low", MediumEffort, "high", XhighEffort, "max", UltraEffort], CollectionOrdering.Matching);
        await Assert.That(CodexModelCatalog.GetReasoningEfforts(Gpt56Terra))
            .IsEquivalentTo(["low", MediumEffort, "high", XhighEffort, "max", UltraEffort], CollectionOrdering.Matching);
        await Assert.That(CodexModelCatalog.GetReasoningEfforts(Gpt56Luna))
            .IsEquivalentTo(["low", MediumEffort, "high", XhighEffort, "max"], CollectionOrdering.Matching);
    }

    /// <summary>Verifies that current non-5.6 Codex models stop at extra-high effort.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Existing_models_stop_at_xhigh()
    {
        string[] expected = ["low", MediumEffort, "high", XhighEffort];
        foreach (string model in new[] { Gpt55, Gpt54, Gpt54Mini, Gpt53CodexSpark })
        {
            await Assert.That(CodexModelCatalog.GetReasoningEfforts(model))
                .IsEquivalentTo(expected, CollectionOrdering.Matching);
        }
    }

    /// <summary>Verifies effort coercion at each capability boundary.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Unsupported_efforts_are_coerced_safely()
    {
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(Gpt56Sol, UltraEffort)).IsEqualTo(UltraEffort);
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(Gpt56Luna, UltraEffort)).IsEqualTo("max");
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(Gpt54Mini, "max")).IsEqualTo(XhighEffort);
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(Gpt55, "minimal")).IsEqualTo(MediumEffort);
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(Gpt54, "none")).IsEqualTo(MediumEffort);
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort("custom-provider-model", UltraEffort)).IsEqualTo(MediumEffort);
    }

    /// <summary>Verifies that retired shipped identifiers do not count as current models.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Deprecated_shipped_models_are_not_current()
    {
        await Assert.That(CodexModelCatalog.IsLegacyModel("gpt-5.3-codex")).IsTrue();
        await Assert.That(CodexModelCatalog.IsLegacyModel("gpt-5.2-codex")).IsTrue();
        await Assert.That(CodexModelCatalog.IsSupportedModel("gpt-5.3-codex")).IsFalse();
        await Assert.That(CodexModelCatalog.IsSupportedModel(Gpt53CodexSpark)).IsTrue();
    }

    /// <summary>Verifies safe defaults for missing and unknown catalog values.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Missing_and_unknown_values_use_safe_defaults()
    {
        await Assert.That(CodexModelCatalog.IsSupportedModel(null)).IsFalse();
        await Assert.That(CodexModelCatalog.IsLegacyModel(null)).IsFalse();
        await Assert.That(CodexModelCatalog.GetReasoningEfforts(null))
            .IsEquivalentTo([MediumEffort], CollectionOrdering.Matching);
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(Gpt56Sol, " ULTRA ")).IsEqualTo(UltraEffort);
        await Assert.That(CodexModelCatalog.ResolveReasoningEffort(null, null)).IsEqualTo(MediumEffort);
    }
}
