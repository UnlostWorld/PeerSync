// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.Glamourer;

using System;
using System.Threading.Tasks;

public class GlamourerCommunicator : PluginCommunicatorBase
{
	private const uint LockCode = 0x3C38C2b1;

	/// <summary>
	/// Return codes for API functions.
	/// </summary>
	public enum GlamourerApiEc
	{
		/// <summary>
		/// The function succeeded.
		/// </summary>
		Success = 0,

		/// <summary>
		/// The function did not encounter a problem, but also did not do anything.
		/// </summary>
		NothingDone = 1,

		/// <summary>
		/// The requested actor was not found.
		/// </summary>
		ActorNotFound = 2,

		/// <summary>
		/// The requested actor was not human, but should have been.
		/// </summary>
		ActorNotHuman = 3,

		/// <summary>
		/// The requested design was not found.
		/// </summary>
		DesignNotFound = 4,

		/// <summary>
		/// The requested item was not found or could not be applied to the requested slot.
		/// </summary>
		ItemInvalid = 5,

		/// <summary>
		/// The state of an actor could not be manipulated because it was locked and the provided key could not unlock it.
		/// </summary>
		InvalidKey = 6,

		/// <summary>
		/// The provided object could not be converted into a valid Glamourer state to apply.
		/// </summary>
		InvalidState = 7,

		/// <summary>
		/// The provided design input could not be parsed.
		/// </summary>
		CouldNotParse = 8,

		/// <summary>
		/// An unknown error occurred.
		/// </summary>
		UnknownError = int.MaxValue,
	}

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
		/// Lock the state with the given key after applying the selected manipulation.
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

	protected override string InternalName => "Glamourer";
	protected override Version Version => new Version(1, 3, 0, 10);

	public async Task<string?> GetState(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return null;

		await Plugin.Framework.RunOnUpdate();

		(GlamourerApiEc status, string? base64) = this.InvokeFunc<(GlamourerApiEc, string?), ushort, uint>(
			"Glamourer.GetStateBase64",
			objectIndex,
			LockCode);

		return base64;
	}

	public async Task SetState(ushort objectIndex, string state, ApplyFlag flags = ApplyFlag.StateDefault)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		this.InvokeFunc<GlamourerApiEc, string, int, uint, ApplyFlag>(
			"Glamourer.ApplyState",
			state,
			objectIndex,
			LockCode,
			flags);
	}

	public async Task RevertState(ushort objectIndex)
	{
		if (!this.GetIsAvailable())
			return;

		await Plugin.Framework.RunOnUpdate();

		this.InvokeFunc<GlamourerApiEc, int, uint, ApplyFlag>(
			"Glamourer.RevertState",
			objectIndex,
			LockCode,
			ApplyFlag.RevertDefault);
	}
}