using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UIU.Simulator.Building.Tests.PlayMode
{
    public sealed class MainSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_LoadsGroundFloorAndKeepsPlayerAtSouthApproach()
        {
            SceneManager.LoadScene("UIU_Main", LoadSceneMode.Single);
            yield return null;

            Scene groundFloor = SceneManager.GetSceneByName("GroundFloor");
            for (int frame = 0; frame < 120 && (!groundFloor.IsValid() || !groundFloor.isLoaded); frame++)
            {
                yield return null;
                groundFloor = SceneManager.GetSceneByName("GroundFloor");
            }

            GameObject player = GameObject.Find("Player");
            GameObject generated = GameObject.Find("Generated");

            Assert.That(groundFloor.IsValid() && groundFloor.isLoaded, Is.True);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.transform.position.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(player.transform.position.z, Is.EqualTo(15f).Within(0.01f));
            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.GetComponentsInChildren<BoxCollider>(true).Length, Is.GreaterThan(100));
        }
    }
}
