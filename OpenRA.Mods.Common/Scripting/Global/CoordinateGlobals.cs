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
using System.Linq;
using Eluant;
using OpenRA.Mods.Common.Traits;
using OpenRA.Scripting;

namespace OpenRA.Mods.Common.Scripting
{
	[ScriptGlobal("CPos")]
	public class CPosGlobal : ScriptGlobal
	{
		public CPosGlobal(ScriptContext context)
			: base(context) { }

		[Desc("Create a new CPos with the specified coordinates on the ground (layer = 0).")]
		public CPos New(int x, int y) { return new CPos(x, y); }

		[Desc("Create a new CPos with the specified coordinates on the specified layer. " +
			"The ground is layer 0, other layers have a unique ID. Examples include tunnels, underground, and elevated bridges.")]
		public CPos NewWithLayer(int x, int y, byte layer)
		{
			if (layer != 0)
			{
				var worldCmls = Context.World.GetCustomMovementLayers();
				if (layer >= worldCmls.Length || worldCmls[layer] == null)
				{
					var layerNames = typeof(CustomMovementLayerType)
						.GetFields()
						.Select(f => (Index: (byte)f.GetRawConstantValue(), f.Name))
						.ToArray();
					var validLayers = new[] { (Index: (byte)0, Name: "Ground") }
						.Concat(worldCmls
							.Where(cml => cml != null)
							.Select(cml => layerNames.Single(ln => ln.Index == cml.Index)));
					throw new LuaException($"Layer {layer} does not exist on this map. " +
						$"Valid layers on this map are: {string.Join(", ", validLayers.Select(x => $"{x.Index} ({x.Name})"))}");
				}
			}

			return new CPos(x, y, layer);
		}

		[Desc("The cell coordinate origin.")]
		public CPos Zero => CPos.Zero;
	}

	[ScriptGlobal("CVec")]
	public class CVecGlobal : ScriptGlobal
	{
		public CVecGlobal(ScriptContext context)
			: base(context) { }

		[Desc("Create a new CVec with the specified coordinates.")]
		public CVec New(int x, int y) { return new CVec(x, y); }

		[Desc("The cell zero-vector.")]
		public CVec Zero => CVec.Zero;
	}

	[ScriptGlobal("WPos")]
	public class WPosGlobal : ScriptGlobal
	{
		public WPosGlobal(ScriptContext context)
			: base(context) { }

		[Desc("Create a new WPos with the specified coordinates.")]
		public WPos New(int x, int y, int z) { return new WPos(x, y, z); }

		[Desc("The world coordinate origin.")]
		public WPos Zero => WPos.Zero;
	}

	[ScriptGlobal("WVec")]
	public class WVecGlobal : ScriptGlobal
	{
		public WVecGlobal(ScriptContext context)
			: base(context) { }

		[Desc("Create a new WVec with the specified coordinates.")]
		public WVec New(int x, int y, int z) { return new WVec(x, y, z); }

		[Desc("The world zero-vector.")]
		public WVec Zero => WVec.Zero;
	}

	[ScriptGlobal("WDist")]
	public class WDistGlobal : ScriptGlobal
	{
		public WDistGlobal(ScriptContext context)
			: base(context) { }

		[Desc("Create a new WDist.")]
		public WDist New(int r) { return new WDist(r); }

		[Desc("Create a new WDist by cell distance.")]
		public WDist FromCells(int numCells) { return WDist.FromCells(numCells); }
	}
}
