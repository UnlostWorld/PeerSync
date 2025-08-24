// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerSync.PluginCommunication;

public class Glamourer : PluginCommunicatorBase
{
	protected override string InternalName => "Glamourer";
	protected override Version Version => new Version(1, 3, 0, 10);

	private readonly uint LockCode = 0x3C38C2b1;

	[Flags]
	public enum ApplyFlag : ulong
	{
		/// <summary>
		/// Apply the selected manipulation only once, without forcing the state into automation.
		/// </summary>
		Once = 0x01,

		///
		/// <summary> Apply the selected manipulation on the equipment (might be more or less supported).
		/// </summary>
		Equipment = 0x02,

		/// <summary>
		/// Apply the selected manipulation on the customizations (might be more or less supported).
		/// </summary>
		Customization = 0x04,

		/// <summary>
		/// Lock the state with the given key after applying the selected manipulation
		/// </summary>
		Lock = 0x08,

		/// <summary>
		/// The default application flags for design-based manipulations.
		/// </summary>
		DesignDefault = ApplyFlag.Once | ApplyFlag.Equipment | ApplyFlag.Customization,

		/// <summary>
		/// The default application flags for state-based manipulations.
		/// </summary>
		StateDefault = ApplyFlag.Equipment | ApplyFlag.Customization | ApplyFlag.Lock,

		/// <summary>
		/// The default application flags for reverse manipulations.
		/// </summary>
		RevertDefault = ApplyFlag.Equipment | ApplyFlag.Customization,
	}

	public async Task<string?> GetState(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		(int status, string? base64) = this.Invoke<(int, string?), ushort, uint>("Glamourer.GetStateBase64", objectIndex, LockCode);
		return base64;
	}

	public async Task SetState(ushort objectIndex, string state, ApplyFlag flags = ApplyFlag.StateDefault)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		int returnCode = this.Invoke<int, string, int, uint, ApplyFlag>("Glamourer.ApplyState", state, objectIndex, LockCode, flags);

		Plugin.Log.Info($"Set State: {objectIndex}\n\n{state}\n\n {returnCode}");

		return;
	}
}