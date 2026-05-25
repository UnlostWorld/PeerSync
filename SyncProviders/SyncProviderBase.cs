// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using PeerSync;
using PeerSync.Connections;

public abstract class SyncProviderBase : IDisposable
{
	private readonly Dictionary<CharacterConnection, SyncProgressBase> perCharacterProgress = new();
	private readonly CancellationTokenSource tokenSource = new();

	public abstract string DisplayName { get; }
	public abstract string Key { get; }

	protected CancellationToken CancellationToken => this.tokenSource.Token;

	public abstract Task<string?> Serialize(Configuration.Character character, ushort objectIndex);
	public abstract Task Deserialize(
		string? lastContent,
		string? content,
		CharacterConnection character,
		ushort objectIndex);

	public virtual void DrawSettings()
	{
	}

	public virtual void DrawStatus()
	{
	}

	public virtual void DrawInspect(CharacterConnection? character, string content)
	{
		if (ImGui.CollapsingHeader(this.DisplayName))
		{
			ImGui.PushFont(UiBuilder.MonoFont);
			ImGui.TextWrapped(content);
			ImGui.PopFont();
		}
	}

	public virtual void Dispose()
	{
		if (!this.tokenSource.IsCancellationRequested)
			this.tokenSource.Cancel();

		this.tokenSource.Dispose();
	}

	public virtual void GetDtrStatus(ref SeStringBuilder dtrEntryBuilder, ref SeStringBuilder dtrTooltipBuilder)
	{
	}

	public virtual void OnCharacterConnected(CharacterConnection character)
	{
		this.perCharacterProgress[character] = this.CreateProgress(character);
	}

	public virtual void OnCharacterDisconnected(CharacterConnection character)
	{
		this.perCharacterProgress.Remove(character);
	}

	public virtual SyncProgressBase? GetProgress(CharacterConnection character)
	{
		this.perCharacterProgress.TryGetValue(character, out SyncProgressBase? value);
		return value;
	}

	public void SetStatus(CharacterConnection character, SyncProgressStatus status)
	{
		SyncProgressBase? progress = this.GetProgress(character);
		if (progress != null)
		{
			progress.Status = status;
		}
	}

	public abstract void Reset(CharacterConnection character, ushort? objectIndex);

	protected virtual SyncProgressBase CreateProgress(CharacterConnection character)
	{
		return new SyncProgressBase(this, character);
	}
}

public abstract class SyncProviderBase<T> : SyncProviderBase
	where T : SyncProgressBase
{
	public new T? GetProgress(CharacterConnection character)
	{
		return base.GetProgress(character) as T;
	}

	protected sealed override SyncProgressBase CreateProgress(CharacterConnection character)
	{
		SyncProgressBase? progress = Activator.CreateInstance(typeof(T), [this, character]) as SyncProgressBase;
		if (progress == null)
			throw new Exception("Failed to create progress type");

		return progress;
	}
}