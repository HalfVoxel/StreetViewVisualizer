using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding.Util;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine.UI;

public class Visualizer : MonoBehaviour {

	public Material lineMaterial;
	public Material surfaceMaterial;
	public float time = 0;
	public float timeScale = 10;
	public Slider timeSlider;
	public Text timeLabel;
	public Text scoreLabel;

	RetainedGizmos gizmos;
	Camera cam;
	Canvas canvas;

	InputData input;
	SubmissionData submission;

	// Use this for initialization
	void Start () {
		gizmos = new RetainedGizmos();
		gizmos.lineMaterial = lineMaterial;
		gizmos.surfaceMaterial = surfaceMaterial;

		timeSlider.onValueChanged.AddListener(value => time = value);

		input = new InputData(File.OpenText(Application.dataPath + "/../tests/paris.txt").ReadToEnd());
		//submission = new SubmissionData(input, File.OpenText(Application.dataPath + "/../test.sub").ReadToEnd());
		submission = new SubmissionData(input, GenerateRandomWalk(input));
		Debug.Log("Maximum possible score: " + MaximumPossibleScore(input));
		Debug.Log("Maximum possible continous score: " + MaximumPossibleContinousScore(input));
		Debug.Log("Optimal time fraction to cover: " + OptimalTimeToCover(input));

		timeSlider.minValue = 0;
		timeSlider.maxValue = input.timeLimit;
		canvas = FindObjectOfType<Canvas>();
		cam = GetComponent<Camera>();
		cam.transform.position = input.bounds.center - Vector3.forward;
		cam.orthographicSize = input.bounds.extents.magnitude * 1.1f;
	}

	static float MaximumPossibleContinousScore (InputData input) {
		return input.cars * (input.timeLimit * (float)input.streets.Sum(s => s.length))/input.streets.Sum(s => s.duration);
	}

	static float OptimalTimeToCover (InputData input) {
		return input.streets.Sum(s => s.duration) / (float)(input.cars * input.timeLimit);
	}

	static int MaximumPossibleScore (InputData input) {
		return input.streets.Sum(s => s.length);
	}

	static string GenerateRandomWalk (InputData input) {
		StringBuilder output = new StringBuilder();
		bool[] taken = new bool[input.streets.Length];
		output.AppendLine(input.cars.ToString());
		for (int i = 0; i < input.cars; i++) {
			int pos = input.startingJunction;
			int time = 0;
			List<int> trace = new List<int>();
			while(true) {
				trace.Add(pos);
				var possibleEdges = input.outEdges[pos].Where(e => !taken[e.index]).ToList();
				if (possibleEdges.Count == 0) possibleEdges = input.outEdges[pos];

				// It's a trap!
				if (possibleEdges.Count == 0) break;

				var edge = possibleEdges[Random.Range(0, possibleEdges.Count)];
				time += edge.duration;
				if (time > input.timeLimit) break;
				taken[edge.index] = true;
				pos = edge.to;
			}

			output.AppendLine(trace.Count.ToString());
			foreach (var p in trace) output.AppendLine(p.ToString());
		}
		return output.ToString();
	}

	void Update () {
		timeLabel.text = string.Format("Time (x{0:0}): {1:0}", timeScale, time);
		timeSlider.value = time;
		scoreLabel.text = submission.ScoreByTime(time).ToString("Score: 0");
		time += Time.deltaTime * timeScale;
	}

	// Update is called once per frame
	void OnPostRender () {
		Render();
	}

	void OnDrawGizmos () {
		if (gizmos != null) Render();
	}

	public static Rect GetScreenCoordinates(RectTransform rectTransform) {
		var worldCorners = new Vector3[4];
		rectTransform.GetWorldCorners(worldCorners);
		var result = new Rect(
			worldCorners[0].x,
			worldCorners[0].y,
			worldCorners[2].x - worldCorners[0].x,
			worldCorners[2].y - worldCorners[0].y);
		return result;
	}

	void RenderBackground () {
		var hasher = new RetainedGizmos.Hasher();
		hasher.AddHash(input.GetHashCode());
		if (!gizmos.Draw(hasher)) {
			Debug.Log("Redrawing background");
			var builder = new RetainedGizmos.Builder();
			foreach (var street in input.streets) {
				builder.DrawLine(input.junctions[street.from], input.junctions[street.to], Color.white);
			}
			builder.Submit(gizmos, hasher);
		}
	}

	void Render () {
		RenderBackground();
		var builder = new RetainedGizmos.Builder();
		if (submission != null) {
			for (int i = 0; i < submission.cars.Length; i++) {
				var car = submission.cars[i];
				var nextIndex = car.NextPathIndex(time);
				Vector2 lastPos = car.Sample(0);
				for (int j = 1; j < nextIndex; j++) {
					var pos = input.junctions[car.path[j]];
					builder.DrawLine(lastPos, pos, Color.green);
					lastPos = pos;
				}
				var currentPos = car.Sample(time);
				builder.DrawLine(lastPos, currentPos, Color.green);
				//builder.DrawWireCube(GraphTransform.identityTransform, new Bounds((Vector3)currentPos + Vector3.forward*0.1f, Vector3.one*0.1f), Color.red);
			}
			for (int i = 0; i < submission.cars.Length; i++) {
				var car = submission.cars[i];
				var currentPos = car.Sample(time);
				builder.DrawCircle((Vector3)currentPos + Vector3.forward*0.1f, 0.004f, Color.red);
				builder.DrawCircle((Vector3)currentPos + Vector3.forward*0.1f, 0.003f, Color.red);
				builder.DrawCircle((Vector3)currentPos + Vector3.forward*0.1f, 0.002f, Color.red);
			}

			var rect = GetScreenCoordinates(timeSlider.GetComponent<RectTransform>());
			rect.y = rect.yMin - 60;
			rect.height = 50;
			var normalizedScores = submission.cumulativeScore
				.Select(v => new Vector2(v.Key / (float)input.timeLimit, v.Value / (float)submission.score))
				.Select(v => cam.ScreenToWorldPoint((Vector3)Rect.NormalizedToPoint(rect, v) + Vector3.forward))
				.ToArray();
			var prev = 0;
			float threshold = 4 * cam.orthographicSize / Screen.width;
			for (int i = 1; i < normalizedScores.Length; i++) {
				if ((normalizedScores[prev] - normalizedScores[i]).sqrMagnitude > threshold*threshold || i == normalizedScores.Length - 1) {
					builder.DrawLine(normalizedScores[prev], normalizedScores[i], Color.white);
					prev = i;
				}
			}
		}

		var hasher = new RetainedGizmos.Hasher();
		hasher.AddHash(Time.realtimeSinceStartup.GetHashCode());
		builder.Submit(gizmos, hasher);
		gizmos.Draw(hasher);
		gizmos.FinalizeDraw();
	}

	class Street {
		public int duration;
		public int length;
		public int from;
		public int to;
		public bool oneWay;
		public int index;
	}

	class InputData {
		public Vector2[] junctions;
		public List<Street>[] outEdges;
		public Street[] streets;
		public int cars;
		public int startingJunction;
		public int timeLimit;

		public Bounds bounds {
			get {
				var bounds = new Bounds(junctions[0], Vector3.zero);
				foreach (var p in junctions) bounds.Encapsulate(p);
				return bounds;
			}
		}

		public InputData (string data) {
			var words = data.Split(new char[0], System.StringSplitOptions.RemoveEmptyEntries);
			//var floats = words.Select(w => float.Parse(w));
			//var ints = words.Select(w => int.Parse(w));
			int wordIndex = 0;
			System.Func<int> nextInt = () => int.Parse(words[wordIndex++]);
			System.Func<float> nextFloat = () => float.Parse(words[wordIndex++]);

			junctions = new Vector2[nextInt()];
			outEdges = new List<Street>[junctions.Length];
			streets = new Street[nextInt()];
			timeLimit = nextInt();
			cars = nextInt();
			startingJunction = nextInt();
			for (int i = 0; i < junctions.Length; i++) {
				var latitude = nextFloat();
				var longitude = nextFloat();
				junctions[i] = new Vector2(latitude, longitude);
				outEdges[i] = new List<Street>();
			}
			// Normalize coordinates and apply proper coordinate transformations for longitude and latitude
			var center = bounds.center;
			var scale = 1 / bounds.size.magnitude;
			for (int i = 0; i < junctions.Length; i++) {
				var lat = junctions[i].x;
				junctions[i] = (junctions[i] - (Vector2)center) * scale;
				var y = junctions[i].x;
				var x = junctions[i].y * Mathf.Cos(lat*Mathf.Deg2Rad);
				junctions[i] = new Vector2(x,y);
			}

			for (int i = 0; i < streets.Length; i++) {
				int ai = nextInt();
				int bi = nextInt();
				int direction = nextInt();
				var street = new Street {
					duration = nextInt(),
					length = nextInt(),
					from = ai,
					to = bi,
					oneWay = direction == 1,
					index = i,
				};
				streets[i] = street;
				outEdges[ai].Add(street);
				if (direction == 2) {
					// Reversed street
					outEdges[bi].Add(new Street {
						duration = street.duration,
						length = street.length,
						from = bi,
						to = ai,
						oneWay = false,
						index = i,
					});
				}
			}
		}
	}

	class CarPath {
		InputData task;
		public int[] path;
		float[] cumulativeTimes;

		public CarPath (InputData task, int[] path) {
			this.path = path;
			this.task = task;
			cumulativeTimes = new float[path.Length];
			for (int i = 1; i < cumulativeTimes.Length; i++) {
				var street = task.outEdges[path[i-1]].Find(s => s.to == path[i]);
				if (street == null) throw new System.Exception("There is no connection between junction " + path[i-1] + " and " + path[i]);

				cumulativeTimes[i] = cumulativeTimes[i-1] + street.duration;
			}
		}

		public int NextPathIndex (float time) {
			if (path.Length == 1) return 0;

			int nextIndex = System.Array.BinarySearch(cumulativeTimes, time);
			if (nextIndex < 0) nextIndex = ~nextIndex;
			return nextIndex;
		}

		public Vector2 Sample (float time) {
			if (path.Length == 1) return task.junctions[path[0]];

			var nextIndex = Mathf.Clamp(NextPathIndex(time), 1, path.Length - 1);
			var relativeTime = Mathf.InverseLerp(cumulativeTimes[nextIndex-1], cumulativeTimes[nextIndex], time);
			return Vector2.Lerp(task.junctions[path[nextIndex-1]], task.junctions[path[nextIndex]], relativeTime);
		}
	}

	class SubmissionData {
		InputData task;
		public CarPath[] cars;
		public int score;
		public KeyValuePair<int, int>[] cumulativeScore;

		public float ScoreByTime (float time) {
			if (time < cumulativeScore[0].Key) return cumulativeScore[0].Value;
			for (int i = 0; i < cumulativeScore.Length - 1; i++) {
				if (time < cumulativeScore[i+1].Key) {
					var relativeTime = Mathf.InverseLerp(cumulativeScore[i].Key, cumulativeScore[i+1].Key, time);
					return Mathf.Lerp(cumulativeScore[i].Value, cumulativeScore[i+1].Value, relativeTime);
				}
			}
			return cumulativeScore.Last().Value;
		}

		public SubmissionData (InputData task, string data) {
			this.task = task;

			var words = data.Split(new char[0], System.StringSplitOptions.RemoveEmptyEntries);
			int wordIndex = 0;
			System.Func<int> nextInt = () => int.Parse(words[wordIndex++]);

			int numCars = nextInt();
			if (numCars != task.cars) throw new System.ArgumentOutOfRangeException();
			cars = new CarPath[numCars];
			for (int i = 0; i < numCars; i++) {
				var path = new int[nextInt()];
				for (int j = 0; j < path.Length; j++) path[j] = nextInt();
				cars[i] = new CarPath(task, path);
			}

			CalculateScore();
		}

		void CalculateScore () {
			int[] coveredTimes = new int[task.streets.Length];
			for (int i = 0; i < coveredTimes.Length; i++) coveredTimes[i] = int.MaxValue;

			List<KeyValuePair<int, int>> scoreDeltas = new List<KeyValuePair<int, int>>();
			for (int i = 0; i < cars.Length; i++) {
				var car = cars[i];
				if (car.path.Length == 0) throw new System.Exception("No positions given for car " + i);
				if (car.path[0] != task.startingJunction) throw new System.Exception("Car " + i + " doesn't start at the starting junction");
				int totalTime = 0;
				for (int j = 0; j < car.path.Length - 1; j++) {
					var street = task.outEdges[car.path[j]].Find(s => s.to == car.path[j+1]);
					if (street == null) throw new System.Exception("There is no connection between junction " + car.path[j] + " and " + car.path[j+1]);
					totalTime += street.duration;
					coveredTimes[street.index] = Mathf.Min(coveredTimes[street.index], totalTime);

				}

				if (totalTime > task.timeLimit) throw new System.Exception("Car " + i + " takes too long time (" + totalTime + " > " + task.timeLimit + ")");
			}

			for (int i = 0; i < coveredTimes.Length; i++) {
				if (coveredTimes[i] < int.MaxValue) {
					scoreDeltas.Add(new KeyValuePair<int, int>(coveredTimes[i], task.streets[i].length));
				}
			}

			scoreDeltas.Insert(0, new KeyValuePair<int, int>(0, 0));
			scoreDeltas.Add(new KeyValuePair<int, int>(task.timeLimit, 0));
			cumulativeScore = scoreDeltas.OrderBy(v => v.Key).ToArray();
			for (int i = 1; i < cumulativeScore.Length; i++) {
				cumulativeScore[i] = new KeyValuePair<int, int>(cumulativeScore[i].Key, cumulativeScore[i].Value + cumulativeScore[i-1].Value);
			}

			score = cumulativeScore.Last().Value;
		}
	}
}
