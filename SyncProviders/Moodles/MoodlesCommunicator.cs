// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace PeerSync.SyncProviders.Moodles;

public class MoodlesCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "Moodles";
	protected override Version Version => new Version(1, 0, 0, 50);

	public string? GetStatusManagerByPC(IPlayerCharacter pc)
	{
		return this.InvokeFunc<string, IPlayerCharacter>(
			"Moodles.GetStatusManagerByPC",
			pc);
	}

	public void SetStatusManagerByPC(IPlayerCharacter pc, string data)
	{
		this.InvokeAction(
			"Moodles.SetStatusManagerByPC",
			pc,
			data);
	}

	public void ClearStatusManager(IPlayerCharacter pc)
	{
		this.InvokeAction(
			"Moodles.ClearStatusManager",
			pc);
	}
}