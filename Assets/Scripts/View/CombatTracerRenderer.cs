using System.Collections.Generic;
using CQRa.Sim.Fog;
using CQRa.Sim.Units;
using Unity.Mathematics;
using UnityEngine;

namespace CQRa.View
{
    /// <summary>
    /// Presentation-only tracer renderer. Drains <see cref="CombatFeed"/> and draws each
    /// shot as a short-lived line so combat reads on screen without any physics or
    /// projectile simulation. Determinism is unaffected: this component never writes
    /// simulation state.
    /// </summary>
    public sealed class CombatTracerRenderer : MonoBehaviour
    {
        private const int PoolSize = 64;
        private const float Lifetime = 0.16f;
        private const float Width = 0.22f;

        private struct Slot
        {
            public LineRenderer Line;
            public float Born;
            public bool Active;
        }

        private readonly List<Slot> _slots = new List<Slot>(PoolSize);
        private Material _material;

        private void Awake()
        {
            CombatFeed.Clear();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            _material = new Material(shader) { name = "CombatTracer" };
            _material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.28f));
            _material.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.14f));

            for (var i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Tracer_{i:00}");
                go.transform.SetParent(transform, false);

                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = Width;
                line.endWidth = Width;
                line.numCapVertices = 2;
                line.sharedMaterial = _material;
                line.startColor = new Color(1f, 0.72f, 0.28f, 1f);
                line.endColor = new Color(1f, 0.86f, 0.52f, 1f);
                line.enabled = false;

                _slots.Add(new Slot { Line = line, Active = false });
            }
        }

        private void Update()
        {
            var now = Time.unscaledTime;

            // Retire finished tracers.
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.Active)
                {
                    continue;
                }

                var age = now - slot.Born;
                if (age >= Lifetime)
                {
                    slot.Active = false;
                    slot.Line.enabled = false;
                    _slots[i] = slot;
                    continue;
                }

                var fade = 1f - age / Lifetime;
                slot.Line.startWidth = Width * fade;
                slot.Line.endWidth = Width * fade;
            }

            // Admit new tracers into free slots.
            var next = 0;
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            var hasFog = false;
            Unity.Entities.Entity fog = Unity.Entities.Entity.Null;
            FogGridInfo fogInfo = default;

            if (world != null && world.IsCreated)
            {
                hasFog = FogQuery.TryGet(world.EntityManager, 0, out fog, out fogInfo);
            }

            var fogCells = hasFog ? FogQuery.GetCells(world.EntityManager, fog) : default;

            while (CombatFeed.TryTake(out var tracer))
            {
                var visible = true;
                if (hasFog && tracer.Team != 0)
                {
                    visible = FogMath.IsVisible(
                        fogCells,
                        fogInfo,
                        new float2(tracer.From.x, tracer.From.z));
                }

                if (!visible)
                {
                    continue;
                }

                var index = FindFreeSlot(ref next);
                if (index < 0)
                {
                    break;
                }

                var slot = _slots[index];
                slot.Line.SetPosition(0, tracer.From);
                slot.Line.SetPosition(1, tracer.To);
                slot.Line.startWidth = Width;
                slot.Line.endWidth = Width;
                slot.Line.enabled = true;
                slot.Born = now;
                slot.Active = true;
                _slots[index] = slot;
            }
        }

        private int FindFreeSlot(ref int cursor)
        {
            for (var step = 0; step < _slots.Count; step++)
            {
                var index = (cursor + step) % _slots.Count;
                if (!_slots[index].Active)
                {
                    cursor = index + 1;
                    return index;
                }
            }

            return -1;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
