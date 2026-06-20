namespace FileTools;

internal static class NameCorrectionFactory
{
    public static KoreanFileNameCorrector Create(FileToolsSettings settings)
    {
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var candidateProfile = RenameCandidateProfileStore.Load(dictionary.CommonPhrases);
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            CandidateProfile = settings.RenameUseDictionary ? candidateProfile : RenameCandidateProfileStore.CreateDefaultDocument(),
            Rules = rules.Rules
        });
    }
}
