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

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Modifies the Cooldown of the GrantPeriodicCondition trait.")]
	public class PeriodicConditionCooldownMultiplierInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		[Desc("Percentage modifier to apply.")]
		public readonly int CooldownModifier = 100;

		public override object Create(ActorInitializer init) { return new PeriodicConditionCooldownMultiplier(this); }
	}

	public class PeriodicConditionCooldownMultiplier : ConditionalTrait<PeriodicConditionCooldownMultiplierInfo>, IPeriodicConditionCooldownModifier
	{
		public PeriodicConditionCooldownMultiplier(PeriodicConditionCooldownMultiplierInfo info)
			: base(info) { }

		int IPeriodicConditionCooldownModifier.GetPeriodicConditionCooldownModifier() { return IsTraitDisabled ? 100 : Info.CooldownModifier; }
	}

	[Desc("Modifies the ActiveDuration of the GrantPeriodicCondition trait.")]
	public class PeriodicConditionActiveMultiplierInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		[Desc("Percentage modifier to apply.")]
		public readonly int ActiveModifier = 100;

		public override object Create(ActorInitializer init) { return new PeriodicConditionActiveMultiplier(this); }
	}

	public class PeriodicConditionActiveMultiplier : ConditionalTrait<PeriodicConditionActiveMultiplierInfo>, IPeriodicConditionActiveModifier
	{
		public PeriodicConditionActiveMultiplier(PeriodicConditionActiveMultiplierInfo info)
			: base(info) { }

		int IPeriodicConditionActiveModifier.GetPeriodicConditionActiveModifier() { return IsTraitDisabled ? 100 : Info.ActiveModifier; }
	}
}
