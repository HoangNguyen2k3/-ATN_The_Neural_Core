using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aircraft {
    public class RaceManager : MonoBehaviour {
        public static RaceManager Ins;
        [Header("Race Settings")]
        [Tooltip("Number of laps for this race")]
        public int numLaps = 2;

        [Tooltip("Bonus seconds to give upon reaching checkpoint")]
        public float checkpointBonusTime = 15f;

        [Serializable]
        public struct DifficultyModel {
            public GameDifficulty difficulty;
            public ModelAsset model;
        }

        public List<DifficultyModel> difficultyModels;

        public AircraftAgent FollowAgent { get; private set; }
        public Camera ActiveCamera { get; private set; }

        private CinemachineVirtualCamera virtualCamera;
        private CountdownUIController countdownUI;
        private HUDController hud;
        private GameoverUIController gameoverUI;
        private AircraftArea aircraftArea;
        private AircraftPlayer aircraftPlayer;

        private List<AircraftAgent> sortedAircraftAgents;

        private float lastResumeTime = 0f;
        private float previouslyElapsedTime = 0f;

        private float lastPlaceUpdate = 0f;

        private Dictionary<AircraftAgent, AircraftStatus> aircraftStatuses;

        private class AircraftStatus {
            public int checkpointIndex = 0;
            public int lap = 0;
            public int place = 0;
            public float timeRemaining = 0f;
        }

        // =============================
        // GAME STATE
        // =============================

        public enum GameState {
            Waiting,
            Playing,
            Paused,
            Gameover
        }

        public GameState CurrentState { get; private set; } = GameState.Waiting;

        public event Action OnStateChange;

        public void SetGameState(GameState newState) {
            if (CurrentState == newState) return;

            CurrentState = newState;
            HandleStateChange();
            OnStateChange?.Invoke();
        }

        // =============================
        // RACE TIME
        // =============================

        public float RaceTime {
            get {
                if (CurrentState == GameState.Playing)
                    return previouslyElapsedTime + Time.time - lastResumeTime;

                if (CurrentState == GameState.Paused)
                    return previouslyElapsedTime;

                return 0f;
            }
        }

        // =============================
        // GETTERS
        // =============================

        public Transform GetAgentNextCheckpoint(AircraftAgent agent) {
            return aircraftArea.list_checkpoint[aircraftStatuses[agent].checkpointIndex].transform;
        }

        public int GetAgentLap(AircraftAgent agent) {
            return aircraftStatuses[agent].lap;
        }

        public string GetAgentPlace(AircraftAgent agent) {
            int place = aircraftStatuses[agent].place;

            if (place <= 0) return "";

            if (place >= 11 && place <= 13) return place + "th";

            switch (place % 10) {
                case 1: return place + "st";
                case 2: return place + "nd";
                case 3: return place + "rd";
                default: return place + "th";
            }
        }

        public float GetAgentTime(AircraftAgent agent) {
            return aircraftStatuses[agent].timeRemaining;
        }

        // =============================
        // UNITY
        // =============================

        private void Awake() {
            Ins = this;
            hud = FindFirstObjectByType<HUDController>();
            countdownUI = FindFirstObjectByType<CountdownUIController>();
            gameoverUI = FindFirstObjectByType<GameoverUIController>();
            virtualCamera = FindFirstObjectByType<CinemachineVirtualCamera>();
            aircraftArea = FindFirstObjectByType<AircraftArea>();
            ActiveCamera = FindFirstObjectByType<Camera>();
        }

        private void Start() {
            FollowAgent = aircraftArea.list_aircraftAgent[0];

            foreach (AircraftAgent agent in aircraftArea.list_aircraftAgent) {
                agent.FreezeAgent();

                if (agent is AircraftPlayer player) {
                    FollowAgent = agent;
                    aircraftPlayer = player;
                    aircraftPlayer.pauseInput.performed += PauseInputPerformed;
                }
                else {
                    var model = difficultyModels
                        .Find(x => x.difficulty == GameManager.Instance.GameDifficultyIsland1).model;

                    agent.SetModel("AI", model);
                }
            }

            virtualCamera.Follow = FollowAgent.transform;
            virtualCamera.LookAt = FollowAgent.transform;

            hud.FollowAgent = FollowAgent;

            hud.gameObject.SetActive(false);
            countdownUI.gameObject.SetActive(false);
            gameoverUI.gameObject.SetActive(false);

            StartCoroutine(StartRace());
        }

        // =============================
        // START RACE
        // =============================

        private IEnumerator StartRace() {
            countdownUI.gameObject.SetActive(true);
            yield return countdownUI.StartCountdown();

            aircraftStatuses = new Dictionary<AircraftAgent, AircraftStatus>();

            foreach (AircraftAgent agent in aircraftArea.list_aircraftAgent) {
                AircraftStatus status = new AircraftStatus();
                status.lap = 1;
                status.timeRemaining = checkpointBonusTime;
                aircraftStatuses.Add(agent, status);
            }

            SetGameState(GameState.Playing);
        }

        // =============================
        // PAUSE
        // =============================

        private void PauseInputPerformed(InputAction.CallbackContext obj) {
            if (CurrentState == GameState.Playing)
                SetGameState(GameState.Paused);
        }

        // =============================
        // STATE HANDLING
        // =============================

        private void HandleStateChange() {
            if (CurrentState == GameState.Playing) {
                lastResumeTime = Time.time;

                hud.gameObject.SetActive(true);

                foreach (AircraftAgent agent in aircraftArea.list_aircraftAgent)
                    agent.ThawAgent();
            }
            else if (CurrentState == GameState.Paused) {
                previouslyElapsedTime += Time.time - lastResumeTime;

                foreach (AircraftAgent agent in aircraftArea.list_aircraftAgent)
                    agent.FreezeAgent();
            }
            else if (CurrentState == GameState.Gameover) {
                previouslyElapsedTime += Time.time - lastResumeTime;

                hud.gameObject.SetActive(false);

                foreach (AircraftAgent agent in aircraftArea.list_aircraftAgent)
                    agent.FreezeAgent();

                gameoverUI.gameObject.SetActive(true);

                // Lưu tiến trình viên đá — chỉ khi player về đích top 2
                if (DataManager.Ins != null && DataManager.Ins.isLoaded && GameManager.Instance != null) {
                    int playerPlace = (aircraftStatuses != null && aircraftStatuses.ContainsKey(FollowAgent))
                        ? aircraftStatuses[FollowAgent].place
                        : 999;
                    if (playerPlace <= 2) {
                        int stoneIndex = GameManager.Instance.numberLevel; // 0=Snow, 1=Desert
                        DataManager.Ins.AddStoneProgress(stoneIndex, GameManager.Instance.DifficultyCountIsland1);
                        AudioManager.Instance?.PlayVictory();
                    }
                    else {
                        AudioManager.Instance?.PlayDefeat();
                    }
                }
            }
        }

        // =============================
        // UPDATE
        // =============================

        private void FixedUpdate() {
            if (CurrentState != GameState.Playing) return;

            if (lastPlaceUpdate + .5f < Time.fixedTime) {
                lastPlaceUpdate = Time.fixedTime;

                if (sortedAircraftAgents == null)
                    sortedAircraftAgents = new List<AircraftAgent>(aircraftArea.list_aircraftAgent);

                sortedAircraftAgents.Sort((a, b) => PlaceComparer(a, b));

                for (int i = 0; i < sortedAircraftAgents.Count; i++)
                    aircraftStatuses[sortedAircraftAgents[i]].place = i + 1;
            }

            foreach (AircraftAgent agent in aircraftArea.list_aircraftAgent) {
                AircraftStatus status = aircraftStatuses[agent];

                if (status.checkpointIndex != agent.NextCheckpointIndex) {
                    status.checkpointIndex = agent.NextCheckpointIndex;
                    status.timeRemaining = checkpointBonusTime;

                    if (status.checkpointIndex == 0) {
                        status.lap++;

                        if (agent == FollowAgent && status.lap > numLaps)
                            SetGameState(GameState.Gameover);
                    }
                }

                status.timeRemaining -= Time.fixedDeltaTime;

                if (status.timeRemaining <= 0f) {
                    aircraftArea.ResetAgentPosition(agent);
                    status.timeRemaining = checkpointBonusTime;
                }
            }
        }

        // =============================
        // PLACE SORT
        // =============================

        private int PlaceComparer(AircraftAgent a, AircraftAgent b) {
            AircraftStatus statusA = aircraftStatuses[a];
            AircraftStatus statusB = aircraftStatuses[b];

            int checkpointA = statusA.checkpointIndex + (statusA.lap - 1) * aircraftArea.list_checkpoint.Count;
            int checkpointB = statusB.checkpointIndex + (statusB.lap - 1) * aircraftArea.list_checkpoint.Count;

            if (checkpointA == checkpointB) {
                Vector3 nextCheckpointPosition = GetAgentNextCheckpoint(a).position;

                return Vector3.Distance(a.transform.position, nextCheckpointPosition)
                    .CompareTo(Vector3.Distance(b.transform.position, nextCheckpointPosition));
            }

            return -checkpointA.CompareTo(checkpointB);
        }

        // =============================
        // CLEANUP
        // =============================

        private void OnDestroy() {
            if (aircraftPlayer != null)
                aircraftPlayer.pauseInput.performed -= PauseInputPerformed;
        }
    }
}