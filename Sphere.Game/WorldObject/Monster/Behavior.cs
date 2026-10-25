using System.Collections.Generic;

namespace SphServer.Sphere.Game.WorldObject;

public partial class Monster
{
	/// <summary>How a kind of monster deviates from the default: chases and hits anyone at 4 m/s.</summary>
	private readonly record struct MonsterBehavior(bool CanChase, bool CanAttack)
	{
		public static readonly MonsterBehavior Default = new(true, true);
	}

	// Town cats (user, 2026-10-04): never attack and, by decision, follow nobody. Players killed them to lower
	// karma. In the original they trailed low-level players slowly; that is left out.
	private static readonly Dictionary<int, MonsterBehavior> BehaviorByGameId = new()
	{
		[1170] = new MonsterBehavior(false, false)
	};

	private MonsterBehavior Behavior => BehaviorByGameId.GetValueOrDefault(DataGameId, MonsterBehavior.Default);
}
