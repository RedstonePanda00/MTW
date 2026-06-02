using Verse;

namespace NCL.Projectiles
{
    public class ModExtension_ProjectileCluster : DefModExtension
    {
        public int primaryDamage = 35;
        public float primaryRadius = 5f;
        public DamageDef primaryDamageDef;

        public ThingDef subProjectileDef;
        public int submunitionCount = 25;
        public float spreadRadiusCells = 10f;
        public float angleJitterDegrees = 4f;
        public float radiusJitterCells = 1f;

        public DamageDef ResolvePrimaryDamageDef(ThingDef projectileDef)
        {
            if (primaryDamageDef != null)
            {
                return primaryDamageDef;
            }

            return projectileDef?.projectile?.damageDef;
        }
    }
}
