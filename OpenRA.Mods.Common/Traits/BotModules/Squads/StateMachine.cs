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

namespace OpenRA.Mods.Common.Traits.BotModules.Squads
{
	sealed class StateMachine
	{
		IState currentState;

		public void Update(Squad squad)
		{
			currentState?.Tick(squad);
		}

		public void ChangeState(Squad squad, IState newState)
		{
			currentState?.Deactivate(squad);

			if (newState != null)
				currentState = newState;

			currentState?.Activate(squad);
		}
	}

	interface IState
	{
		void Activate(Squad bot);
		void Tick(Squad bot);
		void Deactivate(Squad bot);
	}
}
