// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    public sealed class EffectManager : LinkedObject
    {
        private readonly World _world;
        private readonly System.Collections.Generic.Dictionary<uint, MovingEffect> _movingEffectsBySerial = new System.Collections.Generic.Dictionary<uint, MovingEffect>();
        private readonly System.Collections.Generic.Dictionary<uint, TrackedProjectileEffect> _trackedProjectilesBySerial = new System.Collections.Generic.Dictionary<uint, TrackedProjectileEffect>();

        public EffectManager(World world)
        {
            _world = world;
        }

        #region MovingEffect Registration (Legacy)

        /// <summary>
        /// Register a moving effect with its server-assigned serial for collision tracking
        /// </summary>
        public void RegisterMovingEffect(uint serial, MovingEffect effect)
        {
            if (serial != 0 && effect != null)
            {
                _movingEffectsBySerial[serial] = effect;
            }
        }

        /// <summary>
        /// Find a moving effect by its server-assigned serial
        /// </summary>
        public MovingEffect? FindMovingEffectBySerial(uint serial)
        {
            _movingEffectsBySerial.TryGetValue(serial, out MovingEffect? movingEffect);
            return movingEffect;
        }

        /// <summary>
        /// Unregister a moving effect when it's destroyed
        /// </summary>
        public void UnregisterMovingEffect(uint serial)
        {
            if (serial != 0)
            {
                _movingEffectsBySerial.Remove(serial);
            }
        }

        #endregion

        #region TrackedProjectileEffect Registration (Server-Synchronized)

        /// <summary>
        /// Register a tracked projectile effect with its server-assigned serial.
        /// Used for server-synchronized free-aim projectiles.
        /// </summary>
        public void RegisterTrackedProjectile(uint serial, TrackedProjectileEffect effect)
        {
            if (serial != 0 && effect != null)
            {
                effect.ServerSerial = serial;
                _trackedProjectilesBySerial[serial] = effect;
                Log.Trace($"[EffectManager] Registered TrackedProjectileEffect serial={serial}");
            }
        }

        /// <summary>
        /// Find a tracked projectile effect by its server-assigned serial.
        /// </summary>
        public TrackedProjectileEffect? FindTrackedProjectileBySerial(uint serial)
        {
            _trackedProjectilesBySerial.TryGetValue(serial, out TrackedProjectileEffect? effect);
            return effect;
        }

        /// <summary>
        /// Unregister a tracked projectile effect when it's destroyed.
        /// </summary>
        public void UnregisterTrackedProjectile(uint serial)
        {
            if (serial != 0)
            {
                _trackedProjectilesBySerial.Remove(serial);
                Log.Trace($"[EffectManager] Unregistered TrackedProjectileEffect serial={serial}");
            }
        }

        #endregion

        public void Update()
        {
            for (var f = (GameEffect) Items; f != null;)
            {
                var next = (GameEffect) f.Next;

                f.Update();

                if (!f.IsDestroyed && f.Distance > _world.ClientViewRange)
                {
                    f.Destroy();
                }

                f = next;
            }
        }


        public void CreateEffect
        (
            GraphicEffectType type,
            uint source,
            uint target,
            ushort graphic,
            ushort hue,
            ushort srcX,
            ushort srcY,
            sbyte srcZ,
            ushort targetX,
            ushort targetY,
            sbyte targetZ,
            byte speed,
            int duration,
            bool fixedDir,
            bool doesExplode,
            bool hasparticles,
            GraphicEffectBlendMode blendmode
        )
        {
            if (hasparticles)
            {
                Log.Warn("Unhandled particles in an effects packet.");
            }

            GameEffect effect;

            if (hue != 0)
            {
                hue++;
            }

            duration *= Constants.ITEM_EFFECT_ANIMATION_DELAY;

            switch (type)
            {
                case GraphicEffectType.Moving:
                    if (graphic <= 0)
                    {
                        return;
                    }

                    // TODO: speed == 0 means run at standard frameInterval got from anim.mul?
                    if (speed == 0)
                    {
                        speed++;
                    }

                    effect = new MovingEffect
                    (
                        _world,
                        this,
                        source,
                        target,
                        srcX,
                        srcY,
                        srcZ,
                        targetX,
                        targetY,
                        targetZ,
                        graphic,
                        hue,
                        fixedDir,
                        duration,
                        speed
                    )
                    {
                        Blend = blendmode,
                        CanCreateExplosionEffect = doesExplode
                    };

                    break;

                case GraphicEffectType.DragEffect:

                    if (graphic <= 0)
                    {
                        return;
                    }

                    if (speed == 0)
                    {
                        speed++;
                    }

                    effect = new DragEffect
                    (
                        _world,
                        this,
                        source,
                        target,
                        srcX,
                        srcY,
                        srcZ,
                        targetX,
                        targetY,
                        targetZ,
                        graphic,
                        hue,
                        duration,
                        speed
                    )
                    {
                        Blend = blendmode,
                        CanCreateExplosionEffect = doesExplode
                    };

                    break;

                case GraphicEffectType.Lightning:
                    effect = new LightningEffect
                    (
                        _world,
                        this,
                        source,
                        srcX,
                        srcY,
                        srcZ,
                        hue
                    );

                    break;

                case GraphicEffectType.FixedXYZ:

                    if (graphic <= 0)
                    {
                        return;
                    }

                    effect = new FixedEffect
                    (
                        _world,
                        this,
                        srcX,
                        srcY,
                        srcZ,
                        graphic,
                        hue,
                        duration,
                        0 //speed [use 50ms]
                    )
                    {
                        Blend = blendmode
                    };

                    break;

                case GraphicEffectType.FixedFrom:

                    if (graphic <= 0)
                    {
                        return;
                    }

                    effect = new FixedEffect
                    (
                        _world,
                        this,
                        source,
                        srcX,
                        srcY,
                        srcZ,
                        graphic,
                        hue,
                        duration,
                        0 //speed [use 50ms]
                    )
                    {
                        Blend = blendmode
                    };

                    break;

                case GraphicEffectType.ScreenFade:
                    Log.Warn("Unhandled 'Screen Fade' effect.");

                    return;

                default:
                    Log.Warn("Unhandled effect.");

                    return;
            }


            PushToBack(effect);
        }

        public new void Clear()
        {
            var first = (GameEffect) Items;

            while (first != null)
            {
                LinkedObject n = first.Next;

                first.Destroy();

                first = (GameEffect) n;
            }

            Items = null;
        }
    }
}
