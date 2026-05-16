// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Online;

using System.Threading.Tasks;
using System.Text.Json;
using System;
using System.Collections.Generic;

public class GetMembers
{
	public string? GroupFingerprint { get; set; }
	public HashSet<string>? Members { get; set; }

	public async Task<GetMembers?> Send(string indexServer)
	{
		string json = JsonSerializer.Serialize(this);
		string str = await ServerApi.PostAsync($"{indexServer.TrimEnd('/')}/Peer/GetMembers", json, "application/json");

		if (string.IsNullOrEmpty(str))
			return null;

		try
		{
			return JsonSerializer.Deserialize<GetMembers>(str);
		}
		catch (Exception ex)
		{
			throw new Exception($"Error deserializing group status: {str}", ex);
		}
	}
}