﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using com.csutil.model.ecs;
using Xunit;

namespace com.csutil.tests.model.esc {

    public class EntityComponentSystemTests {

        public EntityComponentSystemTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public async Task ExampleUsageOfTemplatesIO() {

            var rootDir = EnvironmentV2.instance.GetOrAddTempFolder("EntityComponentSystemTests_ExampleUsage1");
            var templatesDir = rootDir.GetChildDir("Templates");
            templatesDir.DeleteV2();
            templatesDir.CreateV2();

            var templates = new TemplatesIO<Entity>(templatesDir);

            var enemyTemplate = new Entity() {
                LocalPose = Matrix4x4.CreateTranslation(1, 2, 3),
                Components = new List<IComponentData>() {
                    new EnemyComp() { Id = "c1", Health = 100, Mana = 10 }
                }
            };
            templates.SaveAsTemplate(enemyTemplate);

            // An instance that has a different health value than the template:
            Entity variant1 = templates.CreateVariantInstanceOf(enemyTemplate);
            (variant1.Components.Single() as EnemyComp).Health = 200;
            templates.SaveAsTemplate(variant1); // Save it as a variant of the enemyTemplate

            // Create a variant2 of the variant1
            Entity variant2 = templates.CreateVariantInstanceOf(variant1);
            (variant2.Components.Single() as EnemyComp).Mana = 20;
            templates.SaveAsTemplate(variant2);

            // Updating variant 1 should also update variant2:
            (variant1.Components.Single() as EnemyComp).Health = 300;
            templates.SaveAsTemplate(variant1);
            variant2 = templates.LoadTemplateInstance(variant2.Id);
            Assert.Equal(300, (variant2.Components.Single() as EnemyComp).Health);

            {
                // Another instance that is identical to the template:
                Entity instance3 = templates.CreateVariantInstanceOf(enemyTemplate);
                // instance3 is not saved as a variant 
                // Creating an instance of an instance is not allowed:
                Assert.Throws<InvalidOperationException>(() => templates.CreateVariantInstanceOf(instance3));
                // Instead the parent template should be used to create another instance:
                Entity instance4 = templates.CreateVariantInstanceOf(templates.LoadTemplateInstance(instance3.TemplateId));
                Assert.Equal(instance3.TemplateId, instance4.TemplateId);
                Assert.NotEqual(instance3.Id, instance4.Id);
            }

            var ecs2 = new TemplatesIO<Entity>(templatesDir);

            var ids = ecs2.GetAllEntityIds().ToList();
            Assert.Equal(3, ids.Count());

            Entity v1 = ecs2.LoadTemplateInstance(variant1.Id);
            var enemyComp1 = v1.Components.Single() as EnemyComp;
            Assert.Equal(300, enemyComp1.Health);
            Assert.Equal(10, enemyComp1.Mana);

            // Alternatively to automatically lazy loading the templates can be loaded into memory all at once: 
            await ecs2.LoadAllTemplateFilesIntoMemory();

            Entity v2 = ecs2.LoadTemplateInstance(variant2.Id);
            var enemyComp2 = v2.Components.Single() as EnemyComp;
            Assert.Equal(300, enemyComp2.Health);
            Assert.Equal(20, enemyComp2.Mana);

        }

        [Fact]
        public async Task ExampleUsageOfEcs() {
            // Composing full scene graphs by using the ChildrenIds property:

            var entitiesDir = EnvironmentV2.instance.GetNewInMemorySystem();

            var ecs = new EntityComponentSystem<Entity>(new TemplatesIO<Entity>(entitiesDir));

            await ecs.LoadSceneGraphFromDisk();

            var entityGroup = ecs.Add(new Entity() {
                LocalPose = Matrix4x4.CreateRotationY(MathF.PI / 2) // 90 degree rotation around y axis
            });
            var e1 = entityGroup.AddChild(new Entity() {
                LocalPose = Matrix4x4.CreateRotationY(-MathF.PI / 2), // -90 degree rotation around y axis
            }, AddToChildrenListOfParent);
            var e2 = entityGroup.AddChild(new Entity() {
                LocalPose = Matrix4x4.CreateTranslation(1, 0, 0),
            }, AddToChildrenListOfParent);

            var children = entityGroup.GetChildren();
            Assert.Equal(2, children.Count());
            Assert.Same(e1, children.First());
            Assert.Same(e2, children.Last());
            Assert.Same(e1.GetParent(), entityGroup);
            Assert.Same(e2.GetParent(), entityGroup);

            { // Local and global poses can be accessed like this:
                var rot90Degree = Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, 0, 0);
                Assert.Equal(rot90Degree, entityGroup.GlobalPose().rotation);
                Assert.Equal(rot90Degree, entityGroup.LocalPose().rotation);

                // e2 does not have a local rot so the global rot is the same as of the parent:
                Assert.Equal(rot90Degree, e2.GlobalPose().rotation);
                Assert.Equal(Quaternion.Identity, e2.LocalPose().rotation);

                // e1 has a local rotation that is opposite of the parent 90 degree, the 2 cancel each other out:
                Assert.Equal(Quaternion.Identity, e1.GlobalPose().rotation);
                var rotMinus90Degree = Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2, 0, 0);
                Assert.Equal(rotMinus90Degree, e1.LocalPose().rotation);

                // e1 is in the center of the parent, its global pos isnt affected by the rotation of the parent:
                Assert.Equal(Vector3.Zero, e1.GlobalPose().position);
                Assert.Equal(Vector3.Zero, e1.LocalPose().position);

                // Due to the rotation of the parent the global position of e2 is now (0,0,1):
                Assert.Equal(new Vector3(1, 0, 0), e2.LocalPose().position);
                Assert.Equal(new Vector3(0, 0, 1), e2.GlobalPose().position);

                // The scales are all 1:
                Assert.Equal(Vector3.One, e1.GlobalPose().scale);
                Assert.Equal(Vector3.One, e1.LocalPose().scale);
            }

            Assert.Equal(3, ecs.AllEntities.Count);
            e1.RemoveFromParent(RemoveChildIdFromParent);
            // e1 is removed from its parent but still in the scene graph:
            Assert.Equal(3, ecs.AllEntities.Count);
            Assert.Same(e2, entityGroup.GetChildren().Single());
            Assert.Null(e1.GetParent());
            Assert.True(e1.Destroy(RemoveChildIdFromParent));
            Assert.False(e1.Destroy(RemoveChildIdFromParent));
            // e1 is now fully removed from the scene graph and destroyed:
            Assert.Equal(2, ecs.AllEntities.Count);

            Assert.False(e2.IsDestroyed());
            Assert.True(e2.Destroy(RemoveChildIdFromParent));
            Assert.Equal(1, ecs.AllEntities.Count);
            Assert.Empty(entityGroup.GetChildren());
            Assert.True(e2.IsDestroyed());

        }

        private static Entity AddToChildrenListOfParent(Entity parent, string addedChildId) {
            parent.MutablehildrenIds.Add(addedChildId);
            return parent;
        }

        private Entity RemoveChildIdFromParent(Entity parent, string childIdToRemove) {
            parent.MutablehildrenIds.Remove(childIdToRemove);
            return parent;
        }

        private class Entity : IEntityData {
            public string Id { get; set; } = "" + GuidV2.NewGuid();
            public string TemplateId { get; set; }
            public Matrix4x4? LocalPose { get; set; }
            public IReadOnlyList<IComponentData> Components { get; set; }
            public List<string> MutablehildrenIds { get; } = new List<string>();
            public IReadOnlyList<string> ChildrenIds => MutablehildrenIds;
            public IReadOnlyList<string> Tags { get; set; }

            public string GetId() { return Id; }
        }

        private class EnemyComp : IComponentData {
            public string Id { get; set; }
            public int Mana { get; set; }
            public int Health;
            public string GetId() { return Id; }
        }

    }

}