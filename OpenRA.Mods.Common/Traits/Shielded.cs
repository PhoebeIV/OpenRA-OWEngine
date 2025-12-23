#region Copyright & License Information
/**
 * Copyright (c) The OpenRA Combined Arms Developers (see CREDITS).
 * This file is part of OpenRA Combined Arms, which is free software.
 * It is made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. For more information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.CA.Traits
{
	[Desc("Grants a shield with its own health pool. Main health pool is unaffected by damage until the shield is broken.")]
	public class ShieldedInfo : ConditionalTraitInfo
	{
		[Desc("The strength of the shield (amount of damage it will absorb).")]
		public readonly int MaxStrength = 1000;

		[Desc("Delay in ticks after absorbing damage before the shield will regenerate.")]
		public readonly int RegenDelay = 0;

		[Desc("Amount to recharge at each interval.")]
		public readonly int RegenAmount = 0;

		[Desc("Number of ticks between recharging.")]
		public readonly int RegenInterval = 25;

		[GrantedConditionReference]
		[Desc("Condition to grant when shields are active.")]
		public readonly string ShieldsUpCondition = null;

		[GrantedConditionReference]
		[Desc("Condition to grant when shields are damaged.")]
		public readonly string ShieldsDamagedCondition = null;

		[Desc("DamageTypes that bypass the shield")]
		public readonly BitSet<DamageType> BypassDamageTypes = default;

		[Desc("DamageTypes that bypass the shield")]
		public readonly BitSet<DamageType> OverrideDamageType = default;

		[Desc("Hides selection bar when shield is at max strength.")]
		public readonly bool HideBarWhenFull = false;

		[Desc("Remove shield on trait disabled.")]
		public readonly bool RemoveOnDisable = true;

		public readonly bool ShowSelectionBar = true;
		public readonly Color SelectionBarColor = Color.FromArgb(128, 200, 255);

		public override object Create(ActorInitializer init) { return new Shielded(init, this); }
	}

	sealed class Shielded : ConditionalTrait<ShieldedInfo>, ITick, ISync, ISelectionBar, IDamageModifier, INotifyDamage
	{
		IHealth gethealth;
		readonly Lazy<IShieldRegenModifier[]> shieldModifiers;
		int conditionToken = Actor.InvalidConditionToken;
		int conditionToken2 = Actor.InvalidConditionToken;
		Actor self;

		[VerifySync]
		int strength;
		int intervalTicks;
		int delayTicks;
		int maxshield;

		public Shielded(ActorInitializer init, ShieldedInfo info)
			: base(info)
		{
			self = init.Self;
			shieldModifiers = Exts.Lazy(() => self.TraitsImplementing<IShieldRegenModifier>().ToArray());
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			gethealth = self.Trait<IHealth>();

			maxshield = Info.MaxStrength > 0 ? Info.MaxStrength : gethealth.MaxHP;
			strength = maxshield;
			ResetRegen();
		}

		void ITick.Tick(Actor self)
		{
			if (self.IsDead || IsTraitDisabled)
				return;

			Regenerate(self);
		}

		void Regenerate(Actor self)
		{
			if (strength == maxshield && conditionToken2 != Actor.InvalidConditionToken)
				conditionToken2 = self.RevokeCondition(conditionToken2);
			else if (strength < maxshield && conditionToken2 == Actor.InvalidConditionToken)
				conditionToken2 = self.GrantCondition(Info.ShieldsDamagedCondition);

			if (strength == maxshield)
				return;

			if (--delayTicks > 0)
				return;

			if (--intervalTicks > 0)
				return;

			strength += Info.RegenAmount;

			if (strength > maxshield)
				strength = maxshield;

			if (strength > 0 && conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(Info.ShieldsUpCondition);

			intervalTicks = GetShieldRegenModifier();
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (self.IsDead || IsTraitDisabled)
				return;

			var damageTypes = e.Damage.DamageTypes;
			var overrideTypes = Info.OverrideDamageType;
			var bypassTypes = Info.BypassDamageTypes;

			if (e.Damage.DamageTypes.Overlaps(overrideTypes))
				return;

			if ((strength == 0 && e.Damage.Value > 0) || e.Damage.Value == 0 || e.Attacker == self)
			{
				ResetRegen(); return;
			}

			var damageAmt = Convert.ToInt32(e.Damage.Value / 0.01);
			var excessDamage = damageAmt - strength;
			var health = self.TraitOrDefault<IHealth>();

			if (e.Damage.DamageTypes.Overlaps(bypassTypes))
			{
				if (e.Damage.Value < 0)
					health.InflictDamage(self, e.Attacker, new Damage(e.Damage.Value, overrideTypes), true);
				else
					health.InflictDamage(self, e.Attacker, new Damage(+(e.Damage.Value * 100), overrideTypes), true);
				return;
			}
			else if (e.Damage.Value > 0)
			{
				strength = Math.Max(strength - damageAmt, 0);
				ResetRegen();
			}
			else if (e.Damage.Value < 0)
			{
				strength -= e.Damage.Value;
				if (strength > maxshield)
					strength = maxshield;

				if (health != null)
				{
					var absorbedDamage = new Damage(-e.Damage.Value, damageTypes);
					health.InflictDamage(self, self, absorbedDamage, true);
					return;
				}
			}

			if (health != null)
			{
				var absorbedDamage = new Damage(-e.Damage.Value, damageTypes);
				health.InflictDamage(self, self, absorbedDamage, true);
			}

			if (strength == 0 && conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);

			if (excessDamage > 0)
			{
				var hullDamage = new Damage(excessDamage, damageTypes);

				if (health != null)
					health.InflictDamage(self, e.Attacker, hullDamage, false);
			}
		}

		public int GetShieldRegenModifier()
		{
			return Util.ApplyPercentageModifiers(Info.RegenInterval, shieldModifiers.Value.Select(m => m.GetShieldRegenModifier()));
		}

		public int GetShieldRegenDelayModifier()
		{
			return Util.ApplyPercentageModifiers(Info.RegenDelay, shieldModifiers.Value.Select(m => m.GetShieldRegenModifier()));
		}

		void ResetRegen()
		{
			intervalTicks = GetShieldRegenModifier();
			delayTicks = GetShieldRegenDelayModifier();
		}

		float ISelectionBar.GetValue()
		{
			if (IsTraitDisabled || !Info.ShowSelectionBar || strength == 0 || (strength == maxshield && Info.HideBarWhenFull))
				return 0;

			var selected = self.World.Selection.Contains(self);
			var rollover = self.World.Selection.RolloverContains(self);
			var regularWorld = self.World.Type == WorldType.Regular;
			var statusBars = Game.Settings.Game.StatusBars;

			var displayHealth = selected || rollover || (regularWorld && statusBars == StatusBarsType.AlwaysShow)
				|| (regularWorld && statusBars == StatusBarsType.DamageShow && strength < maxshield);

			if (!displayHealth)
				return 0;

			return (float)strength / maxshield;
		}

		bool ISelectionBar.DisplayWhenEmpty { get { return false; } }

		Color ISelectionBar.GetColor() { return Info.SelectionBarColor; }

		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage)
		{
			return IsTraitDisabled || strength == 0 ? 100 : 1;
		}

		protected override void TraitEnabled(Actor self)
		{
			ResetRegen();

			if (conditionToken == Actor.InvalidConditionToken && strength > 0)
				conditionToken = self.GrantCondition(Info.ShieldsUpCondition);
		}

		protected override void TraitDisabled(Actor self)
		{
			if (conditionToken == Actor.InvalidConditionToken)
				return;

			if (Info.RemoveOnDisable)
				strength = 0;

			conditionToken = self.RevokeCondition(conditionToken);
		}
	}
}
