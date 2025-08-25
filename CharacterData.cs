// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Collections.Generic;

namespace PeerSync;

public class CharacterData(string identifier)
{
	public string Identifier => identifier;
	public Dictionary<string, string?> Syncs { get; init; } = new();
}