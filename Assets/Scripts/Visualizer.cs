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
	public Slider timeScaleSlider;
	public Text timeLabel;
	public Text scoreLabel;
	public InputField inputPath;
	public InputField submissionPath;
	public Button loadButton;
	public Text debugOutput;
	int timeDirection = 1;
	bool paused = false;
	RetainedGizmos gizmos;
	Camera cam;
	Canvas canvas;

	InputData input;
	SubmissionData submission;

	// Use this for initialization
	void Start () {
		inputPath.text = "../tests/paris.txt";
		submissionPath.text = "../solution.sol";

		gizmos = new RetainedGizmos();
		gizmos.lineMaterial = lineMaterial;
		gizmos.surfaceMaterial = surfaceMaterial;

		timeSlider.onValueChanged.AddListener(value => time = value);

		timeSlider.minValue = 0;

		timeScaleSlider.minValue = 0;
		timeScaleSlider.maxValue = 4;
		timeScaleSlider.wholeNumbers = true;
		timeScaleSlider.onValueChanged.AddListener(v => timeScale = paused ? 0 : (int)(timeDirection*Mathf.Pow(10, v)));
		timeScaleSlider.value = 2;

		loadButton.onClick.AddListener(() => Load(inputPath.text, submissionPath.text));
		canvas = FindObjectOfType<Canvas>();
		cam = GetComponent<Camera>();
		Load(inputPath.text, submissionPath.text);
	}

	void Load (string relativeInputPath, string relativeSolutionPath) {
		string inputData, submissionData;
		var cols = loadButton.colors;
		debugOutput.text = "";
		try {
			inputData = File.OpenText(Application.dataPath + "/" + relativeInputPath).ReadToEnd();
			cols.normalColor = Color.white;
			loadButton.colors = cols;
		} catch (System.Exception e) {
			Debug.LogException(e);
			cols.normalColor = Color.red;
			loadButton.colors = cols;
			debugOutput.text = e.Message;
			return;
		}

		try {
			input = new InputData(inputData);
			timeSlider.maxValue = input.timeLimit;
			//submission = new SubmissionData(input, GenerateRandomWalk(input));
			cam.transform.position = input.bounds.center - Vector3.forward;
			cam.orthographicSize = input.bounds.extents.magnitude * 1.1f;
		} catch (System.Exception e) {
			debugOutput.text = e.Message;
			return;
		}

		try {
			submissionData = File.OpenText(Application.dataPath + "/" + relativeSolutionPath).ReadToEnd();
			submission = new SubmissionData(input, submissionData);
			Debug.Log("Score: " + submission.score);
			Debug.Log("Maximum possible score: " + MaximumPossibleScore(input));
			Debug.Log("Maximum possible continous score: " + MaximumPossibleContinousScore(input));
			Debug.Log("Optimal time fraction to cover: " + OptimalTimeToCover(input));
			submissionData = File.OpenText(Application.dataPath + "/" + relativeSolutionPath).ReadToEnd();
		} catch (System.Exception e) {
			Debug.LogException(e);
			cols.normalColor = Color.red;
			loadButton.colors = cols;
			debugOutput.text = e.Message;
			return;
		}

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
		if (Input.GetKeyDown(KeyCode.R)) {
			Load(inputPath.text, submissionPath.text);
		}

		if (Input.GetKeyDown(KeyCode.Space)) {
			paused = !paused;
			timeScaleSlider.onValueChanged.Invoke(timeScaleSlider.value);
		}
		if (Input.GetKeyDown(KeyCode.LeftArrow)) {
			timeDirection = -1;
			timeScaleSlider.onValueChanged.Invoke(timeScaleSlider.value);
		}
		if (Input.GetKeyDown(KeyCode.RightArrow)) {
			timeDirection = 1;
			timeScaleSlider.onValueChanged.Invoke(timeScaleSlider.value);
		}

		if (submission != null) {
			timeLabel.text = string.Format("Time (x{0:0}): {1:0}", timeScale, time);
			timeSlider.value = time;
			scoreLabel.text = submission.ScoreByTime(time).ToString("Score: 0");
			time = Mathf.Clamp(time + Time.deltaTime * timeScale, 0, input.timeLimit);
		}
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
		if (input != null) {
			var hasher = new RetainedGizmos.Hasher();
			hasher.AddHash(input.GetHashCode());
			if (!gizmos.Draw(hasher)) {
				Debug.Log("Redrawing background");
				var builder = new RetainedGizmos.Builder();
				foreach (var street in input.streets) {
					builder.DrawLine(input.junctions[street.from], input.junctions[street.to], Color.white);
					if (street.oneWay) {
						Vector2 dir = input.junctions[street.to] - input.junctions[street.from];
						var length = dir.magnitude;
						var cross = new Vector2(-dir.y, dir.x);
						cross *= 0.08f;
						dir *= 0.08f;
						builder.DrawLine(input.junctions[street.to], input.junctions[street.to] - dir - cross, Color.white);
						builder.DrawLine(input.junctions[street.to], input.junctions[street.to] - dir + cross, Color.white);
					}
				}
				builder.Submit(gizmos, hasher);
			}
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

			for (int i = 0; i < submission.lines.Count; i++) {
				var line = submission.lines[i];
				var offset = Vector3.forward * 0.1f;
				builder.DrawLine((Vector3)input.junctions[line.junction1] + offset, (Vector3)input.junctions[line.junction2] + offset, line.color);
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
			if (cumulativeTimes[cumulativeTimes.Length-1] > task.timeLimit) throw new System.Exception("Time!");
			if (NextPathIndex(task.timeLimit) < path.Length-1) throw new System.Exception(NextPathIndex(task.timeLimit) + " != " + path.Length);
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

	class DebugLine {
		public int junction1, junction2;
		public Color color = Color.red;
	}

	class SubmissionData {
		InputData task;
		public CarPath[] cars;
		public int score;
		public KeyValuePair<int, int>[] cumulativeScore;
		public List<DebugLine> lines = new List<DebugLine>();

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
			var debug = string.Join("\n", data.Split('\n').Where(l => l.StartsWith("DEBUG:")).ToArray());
			data = string.Join("\n", data.Split('\n').Where(l => !l.StartsWith("DEBUG:")).ToArray());
			ParseDebug(debug);
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

		void ParseDebug (string data) {
			foreach (var line in data.Split('\n')) {
				var splits = line.Split(' ');
				if (splits[0] == "DEBUG:LINE") {
					int type = int.Parse(splits[1]);
					if (type == 0) {
						lines.Add(new DebugLine {
							junction1 = int.Parse(splits[2]),
							junction2 = int.Parse(splits[3]),
							color = Color.red
						});
					}
				}
			}
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
				} else {
					/*lines.Add(new DebugLine {
						junction1 = task.streets[i].from,
						junction2 = task.streets[i].to,
						color = Color.blue
					});*/
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
