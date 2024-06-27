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

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	// Type tag for armor type bits
	public class ArmorType { }

	[Desc("Used to define weapon efficiency modifiers with different percentages per Type.")]
	public class ArmorInfo : ConditionalTraitInfo
	{
		[AssetEditor]
		public readonly string Type = null;

		public override object Create(ActorInitializer init) { return new Armor(this); }
	}

	public class Armor : ConditionalTrait<ArmorInfo>
	{
		public Armor(ArmorInfo info)
			: base(info) { }
	}
}
