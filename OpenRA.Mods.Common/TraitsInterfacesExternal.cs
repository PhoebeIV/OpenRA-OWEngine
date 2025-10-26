#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[RequireExplicitImplementation]
	public interface IRemoveInfector
	{
		void RemoveInfector(Actor self, bool kill, AttackInfo e = null);
	}


	[RequireExplicitImplementation]
	public interface IPointDefense
	{
		bool Destroy(WPos position, Player attacker, string type, ProjectileArgs args);
	}

	public interface INotifyPointDefenseHit
	{
		void Hit(int damagePrevented);
	}

	[RequireExplicitImplementation]
	public interface ICrushResource
	{
		void CrushResource(Actor self, CPos cell);
	}

	[RequireExplicitImplementation]
	public interface INotifyPassengersDamage
	{
		void DamagePassengers(int damage, Actor attacker, int amount, Dictionary<string, int> versus, BitSet<DamageType> damageTypes, IEnumerable<int> damageModifiers);
	}
}
