// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace StudioOnline.Sync;

using System.Threading.Tasks;
using System.Text.Json;
using System.Net;
using StudioSync;

public class SyncHeartbeat
{
	public string? Identifier { get; set; }
	public ushort Port { get; set; }
	public string? LocalAddress { get; set; }

	public Task Send()
	{
		string json = JsonSerializer.Serialize(this);
		return ServerApi.PostAsync("Sync/Heartbeat", json, "application/json");
	}
}

public class SyncStatus
{
	public string? Identifier { get; set; }
	public string? Address { get; set; }
	public string? LocalAddress { get; set; }
	public ushort Port { get; set; }

	public async Task<SyncStatus?> Send()
	{
		string json = JsonSerializer.Serialize(this);
		string str = await ServerApi.PostAsync("Sync/Status", json, "application/json");
		Plugin.Log.Information(str);
		return JsonSerializer.Deserialize<SyncStatus>(str);
	}
}