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
	[Desc("Grants a condition when the game is over.")]
	public class GrantConditionOnOwnerWonInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition to grant.")]
		public readonly string Condition = null;

		public override object Create(ActorInitializer init) { return new GrantConditionOnOwnerWon(init.Self, this); }
	}

	public class GrantConditionOnOwnerWon : ConditionalTrait<GrantConditionOnOwnerWonInfo>, INotifyOwnerWon
	{
		readonly GrantConditionOnOwnerWonInfo info;

		public GrantConditionOnOwnerWon(Actor self, GrantConditionOnOwnerWonInfo info)
			: base(info)
		{
			this.info = info;
		}

		void INotifyOwnerWon.OnOwnerWon(Actor self)
		{
			if (IsTraitDisabled)
				return;

			self.GrantCondition(info.Condition);
		}
	}
}
