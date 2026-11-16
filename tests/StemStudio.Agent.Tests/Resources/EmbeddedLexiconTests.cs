using StemStudio.Agent.Resources;

namespace StemStudio.Agent.Tests.Resources;

/// <summary>Skeleton tests (architect, 011 follow-ups): the embedded 009 imitation lexicon v1.</summary>
public sealed class EmbeddedLexiconTests
{
    [Fact]
    public void Lists_AreTheDomainFilesWithoutCommentsOrBlankLines()
    {
        EmbeddedLexicon.Phrases.ShouldContain("in the style of");
        EmbeddedLexicon.Phrases.ShouldContain("in de stijl van");
        EmbeddedLexicon.DenyList.ShouldContain("abba");
        EmbeddedLexicon.GenreWords.ShouldNotBeEmpty();
        EmbeddedLexicon.Phrases.Concat(EmbeddedLexicon.DenyList).Concat(EmbeddedLexicon.GenreWords)
            .ShouldAllBe(entry => entry.Length > 0 && !entry.StartsWith('#') && entry == entry.Trim());
    }

    [Fact]
    public void Lines_ThrowsForAMissingResource()
    {
        Should.Throw<InvalidOperationException>(() => EmbeddedLexicon.Lines("missing.v1.txt")).Message.ShouldContain("missing.v1.txt");
    }
}
