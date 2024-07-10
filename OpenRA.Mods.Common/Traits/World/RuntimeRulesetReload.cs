using System;
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
			if (Enabled && w.GameInfo.IsSinglePlayer)
				Game.RulesetWatcher.ToggleWatching(true);
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

			if (!world.GameInfo.IsSinglePlayer)
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

				TextNotificationsManager.Debug(TranslationProvider.GetString(shouldEnable ? AutoReloadEnabled : AutoReloadDisabled));
			}

			static bool TryParseOnOffBoolean(string value, out bool result)
			{
				result = false;

				value = value.ToLowerInvariant();
				if ("on".Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					result = true;
					return true;
				}
				else if ("off".Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					result = true;
					return true;
				}

				return false;
			}
		}
	}
}
