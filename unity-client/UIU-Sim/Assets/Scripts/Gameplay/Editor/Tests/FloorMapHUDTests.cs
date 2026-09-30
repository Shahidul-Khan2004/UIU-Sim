using NUnit.Framework;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.UI;
using UnityEditor;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class FloorMapHUDTests
    {
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private InteractionController interactionController;
        private GameObject cameraObject;

        private GameObject loaderObject;
        private FloorSceneLoader loader;

        private GameObject mapObject;
        private FloorMapHUD map;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;

            if (FloorMapHUD.Instance != null)
            {
                Object.DestroyImmediate(FloorMapHUD.Instance.gameObject);
            }

            if (GameMenuManager.Instance != null)
            {
                Object.DestroyImmediate(GameMenuManager.Instance.gameObject);
            }

            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<CharacterController>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            interactionController = playerObject.AddComponent<InteractionController>();

            cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();

            loaderObject = new GameObject("TestFloorSceneLoader");
            loader = loaderObject.AddComponent<FloorSceneLoader>();

            mapObject = new GameObject("TestFloorMapHUD");
            map = mapObject.AddComponent<FloorMapHUD>();
        }

        [TearDown]
        public void TearDown()
        {
            if (FloorMapHUD.IsOpen && map != null)
            {
                map.Close();
            }

            Time.timeScale = 1f;

            if (mapObject != null)
            {
                Object.DestroyImmediate(mapObject);
            }

            if (loaderObject != null)
            {
                Object.DestroyImmediate(loaderObject);
            }

            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [TestCase(0, "groundFloor")]
        [TestCase(1, "firstFloor")]
        [TestCase(2, "secondFloor")]
        [TestCase(3, "thirdFloor")]
        [TestCase(4, "thirdFloor")]
        [TestCase(7, "thirdFloor")]
        [TestCase(10, "thirdFloor")]
        public void Test01_FloorNumber_MapsToExpectedPlan(int floorNumber, string expectedResource)
        {
            Assert.That(FloorMapHUD.ResolveMapResourceName(floorNumber), Is.EqualTo(expectedResource));
        }

        [TestCase("groundFloor")]
        [TestCase("firstFloor")]
        [TestCase("secondFloor")]
        [TestCase("thirdFloor")]
        public void Test02_MapTexture_LoadsFromResources_WithUiImportSettings(string resourceName)
        {
            Texture2D texture = Resources.Load<Texture2D>(FloorMapHUD.MapResourceFolder + "/" + resourceName);
            Assert.That(texture, Is.Not.Null, $"Resources/Maps/{resourceName} must be loadable.");

            TextureImporter importer =
                AssetImporter.GetAtPath($"Assets/Resources/Maps/{resourceName}.jpeg") as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None),
                "Power-of-two rescaling would distort the plan's aspect ratio.");
            Assert.That(importer.mipmapEnabled, Is.False);
        }

        [Test]
        public void Test03_Open_ShowsCurrentFloor_AndSoftPausesPlayerOnly()
        {
            loader.CurrentFloorNumber = 0;

            Assert.That(map.TryOpen(), Is.True);

            Assert.That(FloorMapHUD.IsOpen, Is.True);
            Assert.That(map.DisplayedTitleForTesting, Is.EqualTo("GROUND FLOOR"));
            Assert.That(map.DisplayedMapForTesting, Is.Not.Null);
            Assert.That(map.DisplayedMapForTesting.name, Is.EqualTo("groundFloor"));
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(interactionController.enabled, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Soft pause must not use Time.timeScale = 0.");
        }

        [Test]
        public void Test04_UpperFloors_ReuseThirdFloorPlan()
        {
            loader.CurrentFloorNumber = 7;

            Assert.That(map.TryOpen(), Is.True);

            Assert.That(map.DisplayedTitleForTesting, Is.EqualTo("FLOOR 7"));
            Assert.That(map.DisplayedMapForTesting, Is.Not.Null);
            Assert.That(map.DisplayedMapForTesting.name, Is.EqualTo("thirdFloor"));
        }

        [Test]
        public void Test05_ToggleTwice_RestoresPreviousControlState()
        {
            interactionController.enabled = false;

            map.Toggle();
            Assert.That(FloorMapHUD.IsOpen, Is.True);

            map.Toggle();

            Assert.That(FloorMapHUD.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);
            Assert.That(interactionController.enabled, Is.False,
                "Closing the map must not re-enable a control that was already disabled.");
        }

        [Test]
        public void Test06_DoesNotOpen_WhileGameMenuIsOpen()
        {
            GameObject menuObject = new GameObject("TestGameMenuManager");
            GameMenuManager menu = menuObject.AddComponent<GameMenuManager>();
            try
            {
                menu.Open();
                Assert.That(GameMenuManager.IsOpen, Is.True);

                Assert.That(map.TryOpen(), Is.False);
                Assert.That(FloorMapHUD.IsOpen, Is.False);
            }
            finally
            {
                menu.Close(restoreGameplayControls: true);
                Object.DestroyImmediate(menuObject);
            }
        }

        [Test]
        public void Test07_Escape_ClosesMap_InsteadOfOpeningGameMenu()
        {
            GameObject menuObject = new GameObject("TestGameMenuManager");
            GameMenuManager menu = menuObject.AddComponent<GameMenuManager>();
            try
            {
                Assert.That(map.TryOpen(), Is.True);

                menu.HandleEscape();

                Assert.That(FloorMapHUD.IsOpen, Is.False);
                Assert.That(GameMenuManager.IsOpen, Is.False, "Escape must close the map, not open the menu over it.");
                Assert.That(playerMovement.enabled, Is.True);
                Assert.That(firstPersonLook.enabled, Is.True);
            }
            finally
            {
                if (GameMenuManager.IsOpen)
                {
                    menu.Close(restoreGameplayControls: true);
                }

                Object.DestroyImmediate(menuObject);
            }
        }

        [Test]
        public void Test08_DoesNotOpen_WithoutFloorSceneLoader()
        {
            Object.DestroyImmediate(loaderObject);
            loaderObject = null;

            Assert.That(map.TryOpen(), Is.False);
            Assert.That(FloorMapHUD.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True);
        }

        [Test]
        public void Test09_CornerMap_ShowsCurrentFloorDuringGameplay()
        {
            loader.CurrentFloorNumber = 2;

            map.RefreshMiniMap();

            Assert.That(map.IsMiniMapVisibleForTesting, Is.True, "Corner map must show during gameplay.");
            Assert.That(map.MiniMapTitleForTesting, Is.EqualTo("FLOOR 2"));
            Assert.That(map.MiniMapTextureForTesting, Is.Not.Null);
            Assert.That(map.MiniMapTextureForTesting.name, Is.EqualTo("secondFloor"));
        }

        [Test]
        public void Test10_CornerMap_FollowsFloorChanges()
        {
            loader.CurrentFloorNumber = 0;
            map.RefreshMiniMap();
            Assert.That(map.MiniMapTextureForTesting.name, Is.EqualTo("groundFloor"));

            loader.CurrentFloorNumber = 5;
            map.RefreshMiniMap();

            Assert.That(map.MiniMapTitleForTesting, Is.EqualTo("FLOOR 5"));
            Assert.That(map.MiniMapTextureForTesting.name, Is.EqualTo("thirdFloor"));
        }

        [Test]
        public void Test11_CornerMap_HiddenWhileFullscreenOpen_ReturnsAfterClose()
        {
            map.RefreshMiniMap();
            Assert.That(map.IsMiniMapVisibleForTesting, Is.True);

            Assert.That(map.TryOpen(), Is.True);
            Assert.That(map.IsMiniMapVisibleForTesting, Is.False);

            map.Close();
            map.RefreshMiniMap();

            Assert.That(map.IsMiniMapVisibleForTesting, Is.True);
        }

        [Test]
        public void Test12_CornerMap_HiddenOutsideGameplay()
        {
            Object.DestroyImmediate(loaderObject);
            loaderObject = null;
            Object.DestroyImmediate(playerObject);
            playerObject = null;
            cameraObject = null;

            map.RefreshMiniMap();

            Assert.That(map.IsMiniMapVisibleForTesting, Is.False);
        }

        [Test]
        public void Test13_ScrollUp_IncreasesZoom()
        {
            float zoomed = FloorMapHUD.ComputeMapZoom(1f, 120f);

            Assert.That(zoomed, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(zoomed, Is.GreaterThan(1f));
        }

        [Test]
        public void Test14_ScrollDown_DecreasesZoom()
        {
            float zoomed = FloorMapHUD.ComputeMapZoom(2f, -120f);

            Assert.That(zoomed, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(zoomed, Is.LessThan(2f));
        }

        [Test]
        public void Test15_Zoom_ClampsAtMinAndMax()
        {
            Assert.That(FloorMapHUD.ComputeMapZoom(1f, -120f), Is.EqualTo(1f));
            Assert.That(FloorMapHUD.ComputeMapZoom(4f, 120f), Is.EqualTo(4f));
            Assert.That(FloorMapHUD.ComputeMapZoom(3.9f, 1200f), Is.EqualTo(4f));
            Assert.That(FloorMapHUD.ComputeMapZoom(1.1f, -1200f), Is.EqualTo(1f));
        }

        [Test]
        public void Test16_Close_ResetsMapZoomToFit()
        {
            Assert.That(map.TryOpen(), Is.True);
            Assert.That(map.MapZoomForTesting, Is.EqualTo(1f));

            map.ApplyMapScrollForTesting(120f);
            Assert.That(map.MapZoomForTesting, Is.GreaterThan(1f));

            map.Close();

            Assert.That(map.MapZoomForTesting, Is.EqualTo(1f));
        }

        [Test]
        public void Test17_Reopen_StartsAtFitZoom()
        {
            Assert.That(map.TryOpen(), Is.True);
            map.ApplyMapScrollForTesting(120f);
            map.ApplyMapScrollForTesting(120f);
            Assert.That(map.MapZoomForTesting, Is.GreaterThan(1f));

            map.Close();
            Assert.That(map.TryOpen(), Is.True);

            Assert.That(map.MapZoomForTesting, Is.EqualTo(1f),
                "Reopening the fullscreen map must start fitted (zoom 1).");
        }
    }
}
