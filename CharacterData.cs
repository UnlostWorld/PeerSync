// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Text;

namespace PeerSync;

public class CharacterData(string identifier)
{
	public string Identifier => identifier;

	public Dictionary<string, string>? PenumbraFileReplacementHashes { get; set; }
	public string? PenumbraManipulations { get; set; }
	public string? CustomizePlus { get; set; }
	public string? Glamourer { get; set; }
	public string? Heels { get; set; }
	public string? Honorific { get; set; }
	public string? Moodles { get; set; }
	public string? PetNames { get; set; }

	public void Clear()
	{
		this.PenumbraFileReplacementHashes = null;
		this.PenumbraManipulations = null;
		this.CustomizePlus = null;
		this.Glamourer = null;
		this.Heels = null;
		this.Honorific = null;
		this.Moodles = null;
		this.PetNames = null;
	}
}