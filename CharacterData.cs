// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Collections.Generic;

namespace PeerSync;

public class CharacterData()
{
	public string? Fingerprint { get; set; }
	public Dictionary<string, string?> Syncs { get; init; } = new();
	public Dictionary<string, string?> MountOrMinionSyncs { get; init; } = new();
}