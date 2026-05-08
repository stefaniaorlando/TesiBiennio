using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TwoBitMachines.FlareEngine.Interactables
{
        [System.Serializable]
        public class FoliageJobSetup
        {
                [SerializeField] private float frequency = 1f;
                [SerializeField] private float damping = 0.1f;
                [SerializeField] private float uniformity = 0.5f;
                [SerializeField] private float squareDistance = 1f; // square distance of texture size, for collision purposes
                [SerializeField] private float sizeScale = 1f;
                [SerializeField] private float windFrequency = 1f;
                [SerializeField] private float windStrength = 0.25f;

                [System.NonSerialized] private JobHandle jobHandle;
                [System.NonSerialized] private FoliageJob calculateJob;
                [System.NonSerialized] private NativeArray<float> velocity;
                [System.NonSerialized] private NativeArray<float> movement;
                [System.NonSerialized] private NativeArray<float> perlinNoise;
                [System.NonSerialized] private NativeArray<float> vertexOffset;
                [System.NonSerialized] private NativeArray<Vector2> interact;
                [System.NonSerialized] private NativeArray<Vector2> position;
                [System.NonSerialized] private NativeArray<Vector3> characterPosition;
                [System.NonSerialized] private NativeArray<Vector2> characterOldPosition;
                [System.NonSerialized] private NativeArray<Vector2> characterVelocity;

                public void InitializeJob (Foliage foliageClass, List<FoliageInstance> foliage, List<FoliageTexture> textures)
                {
                        sizeScale = 1f / foliageClass.textureArray.size.x; // mesh vertices are spaced apart by size.x, and when movement offset is added, it is added in increments multiplied by sizex. we multiply by inverse to  apply unscaled increments
                        squareDistance = (foliageClass.textureArray.size * 0.5f).sqrMagnitude;
                        velocity = new NativeArray<float>(foliage.Count, Allocator.Persistent);
                        movement = new NativeArray<float>(foliage.Count, Allocator.Persistent);
                        interact = new NativeArray<Vector2>(foliage.Count, Allocator.Persistent);
                        position = new NativeArray<Vector2>(foliage.Count, Allocator.Persistent);
                        perlinNoise = new NativeArray<float>(foliage.Count, Allocator.Persistent);
                        vertexOffset = new NativeArray<float>(foliage.Count, Allocator.Persistent);

                        for (int i = 0; i < foliage.Count; i++)
                        {
                                interact[i] = new Vector2(textures[foliage[i].textureIndex].interact, (int) textures[foliage[i].textureIndex].orientation);
                                position[i] = foliage[i].position;
                                vertexOffset[i] = 0;
                                perlinNoise[i] = 0;
                                velocity[i] = 0;
                                movement[i] = 0;
                        }
                }

                public void CreateJob (Foliage foliage)
                {
                        jobHandle.Complete();

                        if (characterPosition.IsCreated) characterPosition.Dispose();
                        if (characterOldPosition.IsCreated) characterOldPosition.Dispose();
                        if (characterVelocity.IsCreated) characterVelocity.Dispose();

                        List<WorldCollision> character = Character.characters;
                        characterPosition = new NativeArray<Vector3>(character.Count, Allocator.TempJob);
                        characterOldPosition = new NativeArray<Vector2>(character.Count, Allocator.TempJob);
                        characterVelocity = new NativeArray<Vector2>(character.Count, Allocator.TempJob);

                        for (int i = 0; i < character.Count; i++)
                        {
                                Vector3 position = character[i].position;
                                position.z = character[i].box.sizeY;
                                characterPosition[i] = position;
                                characterOldPosition[i] = character[i].oldPosition;
                                characterVelocity[i] = character[i].oldVelocity;
                        }

                        calculateJob = new FoliageJob
                        {
                                jiggle = frequency,
                                damping = damping,
                                position = position,
                                velocity = velocity,
                                movement = movement,
                                frequency = windFrequency,
                                strength = windStrength,
                                uniformity = uniformity,
                                interact = interact,
                                sizeScale = sizeScale,
                                perlinNoise = perlinNoise,
                                vertexOffset = vertexOffset,
                                deltaTime = Time.deltaTime,
                                squareDistance = squareDistance,
                                size = foliage.textureArray.size,
                                playerVelocity = characterVelocity,
                                playerPosition = characterPosition,
                                playerOldPosition = characterOldPosition
                        };
                        jobHandle = calculateJob.Schedule(foliage.textureArray.foliage.Count, 100);
                }

                public void CompleteJob (Foliage foliage)
                {
                        if (Time.deltaTime != 0)
                        {
                                jobHandle.Complete();
                                if (characterPosition.IsCreated)
                                        characterPosition.Dispose();
                                if (characterOldPosition.IsCreated)
                                        characterOldPosition.Dispose();
                                if (characterVelocity.IsCreated)
                                        characterVelocity.Dispose();
                                if (movement.Length == foliage.textureArray.instance_offset.Length)
                                {
                                        movement.CopyTo(foliage.textureArray.instance_offset);
                                        foliage.textureArray.property.SetFloatArray("_Offset", foliage.textureArray.instance_offset);
                                }
                        }
                        //  foliage.textureArray.DrawMeshes ( );
                }

                public void OnDestroy ()
                {
                        jobHandle.Complete();
                        if (characterPosition.IsCreated) characterPosition.Dispose();
                        if (characterOldPosition.IsCreated) characterOldPosition.Dispose();
                        if (characterVelocity.IsCreated) characterVelocity.Dispose();
                        if (vertexOffset.IsCreated)
                                vertexOffset.Dispose();
                        if (perlinNoise.IsCreated)
                                perlinNoise.Dispose();
                        if (position.IsCreated)
                                position.Dispose();
                        if (velocity.IsCreated)
                                velocity.Dispose();
                        if (movement.IsCreated)
                                movement.Dispose();
                        if (interact.IsCreated)
                                interact.Dispose();
                }

        }

}
