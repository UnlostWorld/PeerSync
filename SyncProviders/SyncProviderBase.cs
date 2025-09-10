// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using PeerSync;
using PeerSync.UI;

public abstract class SyncProviderBase : IDisposable
{
	private readonly Dictionary<CharacterSync, SyncProgressBase> perCharacterProgress = new();
	private readonly CancellationTokenSource tokenSource = new();

	public abstract string DisplayName { get; }
	public abstract string Key { get; }

	protected CancellationToken CancellationToken => this.tokenSource.Token;

	public abstract Task<string?> Serialize(ushort objectIndex);
	public abstract Task Deserialize(string? lastContent, string? content, CharacterSync character);
	public virtual void DrawStatus() { }

	public virtual void Dispose()
	{
		if (!this.tokenSource.IsCancellationRequested)
			this.tokenSource.Cancel();

		this.tokenSource.Dispose();
	}

	public virtual void GetDtrStatus(ref SeStringBuilder dtrEntryBuilder, ref SeStringBuilder dtrTooltipBuilder)
	{
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
	Error,
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
			case SyncProgressStatus.Error: return FontAwesomeIcon.ExclamationTriangle;
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