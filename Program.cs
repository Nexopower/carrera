using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

class Program
{
	static void Main(string[] args)
	{
		int size = 20000;
		
		// Pedir tamaño al usuario si no se proporciona por argumentos
		if (args.Length > 0 && int.TryParse(args[0], out var parsed))
		{
			size = parsed;
		}
		else
		{
			Console.Write("Ingrese el tamaño del arreglo: ");
			string? input = Console.ReadLine();
			if (!string.IsNullOrEmpty(input) && int.TryParse(input, out var userSize) && userSize > 0)
			{
				size = userSize;
			}
		}

		Console.WriteLine($"\n=== Simulación de Carrera de Algoritmos Paralelos ===");
		Console.WriteLine($"Tamaño del arreglo: {size:N0} elementos\n");

		// Generar arreglo base
		var rnd = new Random(42);
		var baseArray = new int[size];
		for (int i = 0; i < size; i++) baseArray[i] = rnd.Next(0, size * 10);

		// Valor a buscar (elegimos un elemento existente para evitar búsquedas que siempre fallen)
		int valueToFind = baseArray[rnd.Next(0, size)];
		Console.WriteLine($"Valor a buscar: {valueToFind}");

	// Preparar tareas para cada algoritmo (cada una trabajará sobre una copia independiente)
	var results = new ConcurrentBag<AlgorithmResult>();

	// Preparar un arreglo pre-ordenado para la búsqueda binaria sin tiempo de sort
	var sortedArray = (int[])baseArray.Clone();
	Array.Sort(sortedArray);

	var tasks = new List<Task>
	{
		Task.Run(() => RunSort("BubbleSort", (arr)=>{ BubbleSort(arr); }, baseArray, results)),
		Task.Run(() => RunSort("QuickSort", (arr)=>{ QuickSort(arr,0,arr.Length-1); }, baseArray, results)),
		Task.Run(() => RunSort("InsertionSort", (arr)=>{ InsertionSort(arr); }, baseArray, results)),
		Task.Run(() => RunSearch("SequentialSearch", (arr, val)=> SequentialSearch(arr, val), baseArray, valueToFind, results)),
		Task.Run(() => RunSearchWithPreSort("BinarySearch (con tiempo de ordenamiento)", (arr, val)=> BinarySearch(arr, val), baseArray, valueToFind, results)),
		Task.Run(() => RunSearch("BinarySearch (sin tiempo de ordenamiento)", (arr, val)=> BinarySearch(arr, val), sortedArray, valueToFind, results))
	};

	// Esperar a que todas terminen
	Task.WaitAll(tasks.ToArray());		// Ordenar resultados por tiempo
		var ordered = results.OrderBy(r => r.Elapsed).ToList();

	Console.WriteLine();
	Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════════════════════════╗");
	Console.WriteLine("║                              RESULTADOS DE LA CARRERA                                              ║");
	Console.WriteLine("╠═════╦═══════════════════════════════════════════════╦═════════════╦════════════╦═══════════════════╣");
	Console.WriteLine("║ Pos ║ Algoritmo                                     ║ Tiempo (ms) ║ Memoria    ║ Estado            ║");
	Console.WriteLine("╠═════╬═══════════════════════════════════════════════╬═════════════╬════════════╬═══════════════════╣");
	
	int position = 1;
	foreach (var r in ordered)
	{
		string posStr = position.ToString().PadLeft(3);
		string nameStr = r.Name.PadRight(45);
		string timeStr = r.Elapsed.TotalMilliseconds.ToString("F2").PadLeft(9);
		string memStr = FormatBytes(r.MemoryUsed).PadLeft(8);
		string msgStr = GetShortMessage(r.Message).PadRight(17);
		
		Console.WriteLine($"║ {posStr} ║ {nameStr} ║ {timeStr}   ║ {memStr}  ║ {msgStr} ║");
		position++;
	}
	
	Console.WriteLine("╚═════╩═══════════════════════════════════════════════╩═════════════╩════════════╩═══════════════════╝");		var winner = ordered.First();
		Console.WriteLine();
		Console.WriteLine($"🏆 GANADOR: {winner.Name}");
		Console.WriteLine($"   Tiempo: {winner.Elapsed.TotalMilliseconds:F2} ms | Memoria: {FormatBytes(winner.MemoryUsed)}");

		Console.WriteLine();
		Console.WriteLine("Presiona Enter para salir...");
		Console.ReadLine();
	}

	// Helpers para ejecutar sort y search con medición
	static void RunSort(string name, Action<int[]> sortAction, int[] baseArray, ConcurrentBag<AlgorithmResult> results)
	{
		var arr = (int[])baseArray.Clone();
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		long memBefore = GC.GetTotalMemory(false);
		
		var sw = Stopwatch.StartNew();
		sortAction(arr);
		sw.Stop();
		
		long memAfter = GC.GetTotalMemory(false);
		long memUsed = Math.Max(0, memAfter - memBefore);
		
		results.Add(new AlgorithmResult{name=name, Elapsed=sw.Elapsed, MemoryUsed=memUsed, Message=$"ordenó {arr.Length} elementos"});
	}

	static void RunSearch(string name, Func<int[],int,int> searchFunc, int[] baseArray, int value, ConcurrentBag<AlgorithmResult> results)
	{
		var arr = (int[])baseArray.Clone();
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		long memBefore = GC.GetTotalMemory(false);
		
		var sw = Stopwatch.StartNew();
		int idx = searchFunc(arr, value);
		sw.Stop();
		
		long memAfter = GC.GetTotalMemory(false);
		long memUsed = Math.Max(0, memAfter - memBefore);
		
		results.Add(new AlgorithmResult{name=name, Elapsed=sw.Elapsed, MemoryUsed=memUsed, Message= idx>=0? $"encontró en posición {idx}" : "no encontrado"});
	}

	// Para binary search con presort: medimos INCLUYENDO el tiempo de ordenamiento
	static void RunSearchWithPreSort(string name, Func<int[],int,int> searchFunc, int[] baseArray, int value, ConcurrentBag<AlgorithmResult> results)
	{
		var arr = (int[])baseArray.Clone();
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		long memBefore = GC.GetTotalMemory(false);
		
		var sw = Stopwatch.StartNew();
		Array.Sort(arr); // tiempo incluido
		int idx = searchFunc(arr, value);
		sw.Stop();
		
		long memAfter = GC.GetTotalMemory(false);
		long memUsed = Math.Max(0, memAfter - memBefore);
		
		results.Add(new AlgorithmResult{name=name, Elapsed=sw.Elapsed, MemoryUsed=memUsed, Message= idx>=0? $"encontró en posición {idx}" : "no encontrado"});
	}
	
	// Helper para formatear mensajes cortos en la tabla
	static string GetShortMessage(string message)
	{
		if (message.Contains("ordenó")) return "✓ OK";
		if (message.Contains("encontró")) return "✓ Hallado";
		if (message.Contains("no encontrado")) return "✗ No";
		return "✓";
	}
	
	// Helper para truncar o rellenar strings a un ancho específico
	static string TruncateOrPad(string text, int width)
	{
		if (text.Length > width)
			return text.Substring(0, width - 3) + "...";
		return text.PadRight(width);
	}

	// Estructura de resultado
	class AlgorithmResult
	{
		public string name = "";
		public TimeSpan Elapsed;
		public long MemoryUsed;
		public string Message = "";
		public string Name { get => name; set => name = value; }
	}

	// ---------------- Algoritmos ----------------
	// Búsqueda secuencial
	static int SequentialSearch(int[] arr, int value)
	{
		for (int i = 0; i < arr.Length; i++) if (arr[i] == value) return i;
		return -1;
	}

	// Búsqueda binaria (iterativa)
	static int BinarySearch(int[] arr, int value)
	{
		int lo = 0, hi = arr.Length - 1;
		while (lo <= hi)
		{
			int mid = lo + ((hi - lo) >> 1);
			if (arr[mid] == value) return mid;
			if (arr[mid] < value) lo = mid + 1; else hi = mid - 1;
		}
		return -1;
	}

	// Bubble Sort
	static void BubbleSort(int[] arr)
	{
		int n = arr.Length;
		bool swapped;
		for (int i = 0; i < n - 1; i++)
		{
			swapped = false;
			for (int j = 0; j < n - i - 1; j++)
			{
				if (arr[j] > arr[j + 1])
				{
					int tmp = arr[j]; arr[j] = arr[j + 1]; arr[j + 1] = tmp;
					swapped = true;
				}
			}
			if (!swapped) break;
		}
	}

	// Insertion Sort
	static void InsertionSort(int[] arr)
	{
		for (int i = 1; i < arr.Length; i++)
		{
			int key = arr[i];
			int j = i - 1;
			while (j >= 0 && arr[j] > key)
			{
				arr[j + 1] = arr[j];
				j--;
			}
			arr[j + 1] = key;
		}
	}

	// QuickSort (in-place)
	static void QuickSort(int[] arr, int low, int high)
	{
		if (low >= high) return;
		int p = Partition(arr, low, high);
		QuickSort(arr, low, p - 1);
		QuickSort(arr, p + 1, high);
	}

	static int Partition(int[] arr, int low, int high)
	{
		int pivot = arr[high];
		int i = low - 1;
		for (int j = low; j < high; j++)
		{
			if (arr[j] <= pivot)
			{
				i++;
				int tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
			}
		}
		int t = arr[i + 1]; arr[i + 1] = arr[high]; arr[high] = t;
		return i + 1;
	}

	static string FormatBytes(long bytes)
	{
		if (bytes < 0) return "0 B";
		string[] suf = { "B", "KB", "MB", "GB" };
		int i = 0;
		double dbl = bytes;
		while (dbl >= 1024 && i < suf.Length - 1) { dbl /= 1024; i++; }
		return $"{dbl:F2} {suf[i]}";
	}
}
