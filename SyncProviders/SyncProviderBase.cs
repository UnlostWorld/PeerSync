// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using PeerSync;
using PeerSync.UI;

public abstract class SyncProviderBase : IDisposable
{
	private readonly Dictionary<CharacterSync, SyncProgressBase> perCharacterProgress = new();

	public bool IsDisposed { get; private set; }

	public abstract string DisplayName { get; }
	public abstract string Key { get; }

	public abstract Task<string?> Serialize(ushort objectIndex);
	public abstract Task Deserialize(string? lastContent, string? content, CharacterSync character);
	public virtual void DrawStatus() { }

	public virtual void Dispose()
	{
		this.IsDisposed = true;
	}

	public virtual void OnCharacterConnected(CharacterSync character)
	{
		this.perCharacterProgress[character] = this.CreateProgress(character);
	}

	public virtual void OnCharacterDisconnected(CharacterSync character)
	{
		this.perCharacterProgress.Remove(character);
	}

	public virtual SyncProgressBase? GetProgress(CharacterSync character)
	{
		this.perCharacterProgress.TryGetValue(character, out SyncProgressBase? value);
		return value;
	}

	public void SetStatus(CharacterSync character, SyncProgressStatus status)
	{
		SyncProgressBase? progress = this.GetProgress(character);
		if (progress != null)
		{
			progress.Status = status;
		}
	}

	protected virtual SyncProgressBase CreateProgress(CharacterSync character)
	{
		return new SyncProgressBase(this);
	}
}

public abstract class SyncProviderBase<T> : SyncProviderBase
	where T : SyncProgressBase
{
	public new T? GetProgress(CharacterSync character)
	{
		return base.GetProgress(character) as T;
	}

	protected sealed override SyncProgressBase CreateProgress(CharacterSync character)
	{
		SyncProgressBase? progress = Activator.CreateInstance(typeof(T), [this]) as SyncProgressBase;
		if (progress == null)
			throw new Exception("Failed to create progress type");

		return progress;
	}
}

public enum SyncProgressStatus
{
	None,
	Syncing,
	Applied,
	Empty,
	NotApplied,
}

public static class SyncProgressStatusExtensions
{
	public static FontAwesomeIcon GetIcon(this SyncProgressStatus status)
	{
		switch (status)
		{
			case SyncProgressStatus.None: return FontAwesomeIcon.None;
			case SyncProgressStatus.Syncing: return FontAwesomeIcon.Sync;
			case SyncProgressStatus.Applied: return FontAwesomeIcon.Check;
			case SyncProgressStatus.Empty: return FontAwesomeIcon.None;
			case SyncProgressStatus.NotApplied: return FontAwesomeIcon.Times;
		}

		return FontAwesomeIcon.None;
	}
}

public class SyncProgressBase(SyncProviderBase provider)
{
	public SyncProviderBase Provider = provider;

	public SyncProgressStatus Status { get; set; }
	public long Current { get; set; }
	public long Total { get; set; }

	public virtual void DrawInfo()
	{
		if (this.Current < this.Total && this.Total > 0)
		{
			float p = (float)this.Current / (float)this.Total;
			ImGuiEx.ThinProgressBar(p, -1);
		}
	}

	public virtual void DrawStatus()
	{
		ImGuiEx.Icon(this.Status.GetIcon());
	}
}