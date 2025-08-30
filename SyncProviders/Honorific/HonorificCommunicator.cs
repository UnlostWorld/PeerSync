// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;

namespace PeerSync.SyncProviders.Honorific;

public class HonorificCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "Honorific";
	protected override Version Version => new(1, 6, 0, 0);

	public string? GetCharacterTitle(int objectIndex)
	{
		return this.InvokeFunc<string, int>("Honorific.GetCharacterTitle", objectIndex);
	}

	public void SetCharacterTitle(int objectIndex, string titleData)
	{
		this.InvokeAction("Honorific.SetCharacterTitle", objectIndex, titleData);
	}

	public void ClearCharacterTitle(int objectIndex)
	{
		this.InvokeAction("Honorific.ClearCharacterTitle", objectIndex);
	}
}