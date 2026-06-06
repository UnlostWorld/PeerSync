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
	private readonly CancellationTokenSource tokenSource = new();

	public abstract string DisplayName { get; }
	public abstract string Key { get; }

	protected CancellationToken CancellationToken => this.tokenSource.Token;

	public abstract Task<string?> Serialize(Configuration.Character character, ushort objectIndex);

	public virtual Task Prepare(
		string? content,
		CharacterConnection character,
		SyncProgressBase progress)
	{
		return Task.CompletedTask;
	}

	public abstract SyncProgressStatus Apply(
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
	}

	public virtual void OnCharacterDisconnected(CharacterConnection character)
	{
	}

	public abstract void Reset(CharacterConnection character, ushort? objectIndex);

	public virtual SyncProgressBase CreateProgress(CharacterConnection character)
	{
		return new SyncProgressBase(this, character);
	}
}

public abstract class SyncProviderBase<T> : SyncProviderBase
	where T : SyncProgressBase
{
	public sealed override SyncProgressBase CreateProgress(CharacterConnection character)
	{
		SyncProgressBase? progress = Activator.CreateInstance(typeof(T), [this, character]) as SyncProgressBase;
		if (progress == null)
			throw new Exception("Failed to create progress type");

		return progress;
	}

	public sealed override Task Prepare(
		string? content,
		CharacterConnection character,
		SyncProgressBase progress)
	{
		if (progress is not T tProgress)
			throw new Exception("Wrong progress type for provider");

		return this.Prepare(content, character, tProgress);
	}

	public virtual Task Prepare(
		string? content,
		CharacterConnection character,
		T progress)
	{
		return Task.CompletedTask;
	}
}