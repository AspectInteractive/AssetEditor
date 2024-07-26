#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Commands;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Handles ruleset reloading at runtime.")]
	public class RuntimeRulesetReloadInfo : TraitInfo
	{
		public override object Create(ActorInitializer init)
		{
			return new RuntimeRulesetReload();
		}
	}

	public class RuntimeRulesetReload : IChatCommand, IWorldLoaded, IPostWorldLoaded
	{
		[TranslationReference]
		const string CheatsDisabled = "notification-cheats-disabled";

		[TranslationReference]
		const string CommandDescription = "description-ruleset-autoreload";

		const string CommandName = "autoreload";

		[TranslationReference]
		const string AutoReloadEnabled = "notification-ruleset-autoreload-enabled";

		[TranslationReference]
		const string AutoReloadDisabled = "notification-ruleset-autoreload-disabled";

		[TranslationReference]
		const string CommandArgumentError = "notification-ruleset-autoreload-invalid-argument";

		[TranslationReference]
		const string AutoReloadMultiplayerDisabled = "notification-ruleset-autoreload-multiplayer-disabled";

		World world;
		DeveloperMode developerMode;

		public bool Enabled
		{
			get => Game.Settings.Debug.EnableRulesetAutoReload;
			set => Game.Settings.Debug.EnableRulesetAutoReload = value;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			world = w;

			if (world.LocalPlayer != null)
				developerMode = world.LocalPlayer.PlayerActor.Trait<DeveloperMode>();

			var console = w.WorldActor.TraitOrDefault<ChatCommands>();
			var help = w.WorldActor.TraitOrDefault<HelpCommand>();

			if (console == null || help == null)
				return;

			console.RegisterCommand(CommandName, this);
			help.RegisterHelp(CommandName, CommandDescription);
		}

		void IPostWorldLoaded.PostWorldLoaded(World w, WorldRenderer wr)
		{
			if (Enabled && world.LobbyInfo.GlobalSettings.GameSavesEnabled)
			{
				Game.RulesetWatcher?.Dispose();
				Game.RulesetWatcher = new RulesetWatcher(w, Game.ModData);
			}
		}

		void IChatCommand.InvokeCommand(string name, string arg)
		{
			if (world.LocalPlayer == null)
				return;

			if (!developerMode.Enabled)
			{
				TextNotificationsManager.Debug(TranslationProvider.GetString(CheatsDisabled));
				return;
			}

			if (!world.LobbyInfo.GlobalSettings.GameSavesEnabled)
			{
				TextNotificationsManager.Debug(TranslationProvider.GetString(AutoReloadMultiplayerDisabled));
				return;
			}

			if (name == CommandName)
			{
				bool shouldEnable;
				if (string.IsNullOrEmpty(arg))
					shouldEnable = !Enabled;
				else if (!TryParseOnOffBoolean(arg.Trim(), out shouldEnable))
				{
					TextNotificationsManager.Debug(TranslationProvider.GetString(CommandArgumentError));
					return;
				}

				Enabled = shouldEnable;

				if (shouldEnable)
				{
					Game.RulesetWatcher?.Dispose();
					Game.RulesetWatcher = new RulesetWatcher(world, Game.ModData);
				}
				else
				{
					Game.RulesetWatcher?.Dispose();
					Game.RulesetWatcher = null;
				}

				TextNotificationsManager.Debug(TranslationProvider.GetString(shouldEnable ? AutoReloadEnabled : AutoReloadDisabled));
			}

			static bool TryParseOnOffBoolean(string value, out bool result)
			{
				result = false;

				if (value.Equals("on", StringComparison.InvariantCultureIgnoreCase))
				{
					result = true;
					return true;
				}
				else if (value.Equals("off", StringComparison.InvariantCultureIgnoreCase))
					return true;

				return false;
			}
		}
	}
}
