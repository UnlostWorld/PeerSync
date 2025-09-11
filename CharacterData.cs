// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System.Collections.Generic;
using Newtonsoft.Json;

public class CharacterData()
{
	[JsonProperty("F")]
	public string? Fingerprint { get; set; }

	[JsonProperty("C")]
	public Dictionary<string, string?> Character { get; init; } = new();

	[JsonProperty("M")]
	public Dictionary<string, string?> MountOrMinion { get; init; } = new();

	[JsonProperty("P")]
	public Dictionary<string, string?> Pet { get; init; } = new();

	public void Clear()
	{
		this.Character.Clear();
		this.MountOrMinion.Clear();
		this.Pet.Clear();
	}
}