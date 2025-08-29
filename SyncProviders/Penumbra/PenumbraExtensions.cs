// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using Penumbra.Api.Enums;

namespace PeerSync.SyncProviders.Penumbra;

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