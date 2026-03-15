using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
namespace Aircraft {
    public class AircraftArea : MonoBehaviour {
        [Tooltip("The path the race will take")]
        public CinemachineSmoothPath racePath;
        [Tooltip("The prefab to use for checkpoint")]
        public GameObject checkpointPrefab;
        [Tooltip("The prefab to use for start/end checkpoint")]
        public GameObject finishCheckpointPrefab;
        [Tooltip("Choose training mode")]
        public bool isTrainingMode;

        [Header("=================MenuScene==================")]
        public Transform airCraftTarget;
        public List<AircraftAgent> list_aircraftAgent { get; private set; }
        public List<GameObject> list_checkpoint { get; private set; }
        private void Awake() {
            if (list_aircraftAgent == null)
                FindAircraftAgent();
        }


        private void Start() {
            if (list_checkpoint == null)
                CreateCheckpoints();
        }
        private void FindAircraftAgent() {
            list_aircraftAgent = transform.GetComponentsInChildren<AircraftAgent>().ToList();
            Debug.Assert(list_aircraftAgent.Count > 0, "No AircraftAgents found");
        }

        private void CreateCheckpoints() {
            Debug.Assert(racePath != null, "Race Path was not set");
            list_checkpoint = new();
            int numCheckPoint = (int)racePath.MaxUnit(CinemachinePathBase.PositionUnits.PathUnits);
            for (int i = 0; i < numCheckPoint; i++) {
                GameObject checkpoint;
                if (i == numCheckPoint - 1) {
                    checkpoint = Instantiate(finishCheckpointPrefab);
                }
                else {
                    checkpoint = Instantiate(checkpointPrefab);
                }
                checkpoint.transform.SetParent(racePath.transform);
                checkpoint.transform.localPosition = racePath.m_Waypoints[i].position;
                checkpoint.transform.rotation = racePath.EvaluateOrientationAtUnit(i, CinemachinePathBase.PositionUnits.PathUnits);
                list_checkpoint.Add(checkpoint);
            }
        }

        public void ResetAgentPosition(AircraftAgent agent, bool randomize = false) {
            if (list_aircraftAgent == null)
                FindAircraftAgent();
            if (list_checkpoint == null)
                CreateCheckpoints();
            if (randomize) {
                agent.NextCheckpointIndex = Random.Range(0, list_checkpoint.Count);
            }
            int previousCheckpointIndex = agent.NextCheckpointIndex - 1;
            if (previousCheckpointIndex == -1) {
                previousCheckpointIndex = list_checkpoint.Count - 1;
            }
            float startPosition = racePath.FromPathNativeUnits(previousCheckpointIndex, CinemachinePathBase.PositionUnits.PathUnits);
            Vector3 basePosition = racePath.EvaluatePosition(startPosition);
            Quaternion oriention = racePath.EvaluateOrientation(startPosition);
            Vector3 positionOffset = Vector3.right * (list_aircraftAgent.IndexOf(agent) - list_aircraftAgent.Count / 2) * Random.Range(9f, 10f);
            agent.transform.position = basePosition + oriention * positionOffset;
            agent.transform.rotation = oriention;
        }
    }
}
