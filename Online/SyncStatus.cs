// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Online;

using System.Threading.Tasks;
using System.Text.Json;

public class SyncStatus
{
	public string? Identifier { get; set; }
	public string? Address { get; set; }
	public string? LocalAddress { get; set; }
	public ushort Port { get; set; }

	public async Task<SyncStatus?> Send(string indexServer)
	{
		string json = JsonSerializer.Serialize(this);
		string str = await ServerApi.PostAsync($"{indexServer}/Status", json, "application/json");
		return JsonSerializer.Deserialize<SyncStatus>(str);
	}
}