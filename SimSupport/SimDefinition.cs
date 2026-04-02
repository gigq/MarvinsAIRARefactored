namespace MarvinsAIRARefactored.SimSupport;

public sealed record SimDefinition(
	SimId Id,
	string DisplayName,
	string FolderName,
	SimSupportLevel SupportLevel,
	SimFeature Features,
	bool UsesLegacyDocumentsLayout,
	string? WindowTitle,
	string SupportSummary )
{
	public bool Supports( SimFeature feature )
	{
		return ( Features & feature ) == feature;
	}
}
