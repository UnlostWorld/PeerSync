// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Collections.Generic;

namespace PeerSync;

public class CharacterData()
{
	public string? Identifier { get; set; }
	public Dictionary<string, string?> Syncs { get; init; } = new();
}