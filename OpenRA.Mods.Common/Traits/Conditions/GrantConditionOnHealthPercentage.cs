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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Applies a condition to the actor at when its health is between 2 specific values.")]
	public class GrantConditionOnHealthPercentageInfo : ConditionalTraitInfo, Requires<IHealthInfo>
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = null;

		[Desc("Play a random sound from this list when enabled.")]
		public readonly string[] EnabledSounds = [];

		[Desc("Play a random sound from this list when disabled.")]
		public readonly string[] DisabledSounds = [];

		[Desc("Minimum level of health at which to grant the condition.")]
		public readonly int MinHP = 0;

		[Desc("Maximum level of health at which to grant the condition.")]
		public readonly int MaxHP = 100;

		[Desc("Is the condition irrevocable once it has been granted?")]
		public readonly bool GrantPermanently = false;

		public override object Create(ActorInitializer init) { return new GrantConditionOnHealthPercentage(init.Self, this); }
	}

	public class GrantConditionOnHealthPercentage : ConditionalTrait<GrantConditionOnHealthPercentageInfo>, INotifyCreated, INotifyDamage
	{
		readonly GrantConditionOnHealthPercentageInfo info;
		readonly IHealth health;
		readonly int maxHP;

		int conditionToken = Actor.InvalidConditionToken;

		public GrantConditionOnHealthPercentage(Actor self, GrantConditionOnHealthPercentageInfo info)
			: base(info)
		{
			this.info = info;
			health = self.Trait<IHealth>();
			maxHP = info.MaxHP;
		}

		void INotifyCreated.Created(Actor self)
		{
			if (!IsTraitDisabled)
				GrantConditionOnValidHealth(self, health.HP);
		}

		protected override void TraitEnabled(Actor self)
		{
			GrantConditionOnValidHealth(self, health.HP);
		}

		protected override void TraitDisabled(Actor self)
		{
			if (Info.GrantPermanently || conditionToken == Actor.InvalidConditionToken)
				return;

			conditionToken = self.RevokeCondition(conditionToken);
		}

		void GrantConditionOnValidHealth(Actor self, int hp)
		{
			if (hp >= info.MaxHP * (long)health.MaxHP / 100 || hp < info.MinHP * (long)health.MaxHP / 100 || conditionToken != Actor.InvalidConditionToken)
				return;

			conditionToken = self.GrantCondition(info.Condition);

			var sound = info.EnabledSounds.RandomOrDefault(Game.CosmeticRandom);
			Game.Sound.Play(SoundType.World, sound, self.CenterPosition);
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			var granted = conditionToken != Actor.InvalidConditionToken;
			if (self.IsDead || IsTraitDisabled)
				return;

			if (granted && info.GrantPermanently)
				return;

			if (!granted)
				GrantConditionOnValidHealth(self, health.HP);
			else if (granted && (health.HP >= info.MaxHP * (long)health.MaxHP / 100 || health.HP < info.MinHP * (long)health.MaxHP / 100))
			{
				conditionToken = self.RevokeCondition(conditionToken);

				var sound = info.DisabledSounds.RandomOrDefault(Game.CosmeticRandom);
				Game.Sound.Play(SoundType.World, sound, self.CenterPosition);
			}
		}
	}
}
