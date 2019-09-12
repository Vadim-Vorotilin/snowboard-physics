using System;
using System.Collections.Generic;
using UnityEngine;

public class Delayer : MonoBehaviour {

    private static Delayer _instance;

    private static Delayer Instance {
        get {
            return _instance ?? (_instance = new GameObject("Delayer").AddComponent<Delayer>());
        }
    }

    private List<KeyValuePair<float, Action>> _actions = new List<KeyValuePair<float, Action>>();

    void Awake() {
        DontDestroyOnLoad(this);
    }

    void Update() {
        int i = 0;

        while (i < _actions.Count) {
            if (_actions[i].Key <= Time.time) {
                _actions[i].Value();
                _actions.RemoveAt(i);
            } else {
                i++;
            }
        }
    }

    public static void ExecuteAfter(Action action, float seconds) {
        if (action == null)
            throw new ArgumentNullException("action");

        if (seconds <= 0) {
            if (action != null)
                action();
        } else {
            Instance._actions.Add(new KeyValuePair<float, Action>(Time.time + seconds, action));
        }
    }

}