// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Penumbra;

using global::Penumbra.Api.Enums;

public static class PenumbraExtensions
{
	public static void ThrowOnFailure(this PenumbraApiEc code)
	{
		switch (code)
		{
			case PenumbraApiEc.Success:
			case PenumbraApiEc.NothingChanged:
				return;

			default:
				throw new System.Exception($"Penumbra error: {code}");
		}
	}
}