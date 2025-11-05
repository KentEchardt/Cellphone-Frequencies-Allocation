using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WIM.FrequencyAllocator
{
    /// <summary>
    /// Represents a single Cell Tower.
    /// </summary>
    public record CellTower(string CellID, double Latitude, double Longitude);

    /// <summary>
    /// Main program for allocating cell tower frequencies.
    /// </summary>
    public class Program
    {
        // I've chosen 500 meters (0.5km) as a default.
        private const double INTERFERENCE_RADIUS_KM = 0.5;

        // The available "colours" for our graph.
        private static readonly int[] AvailableFrequencies = { 110, 111, 112, 113, 114, 115 };

        /// <summary>
        /// Main entry point for the command-line application.
        /// </summary>
        /// <param name="args">Command-line arguments. Expects an optional file path.</param>
        public static void Main(string[] args)
        {
            Console.WriteLine("--- Cell Tower Frequency Allocator ---");

            // Allow the CSV file path to be passed as an argument.
            // Defaults to "towers.csv" in the same directory.
            string filePath = args.Length > 0 ? args[0] : "towers.csv";

            // --- Check if the user specified a file that doesn't exist ---
            // But the default "towers.csv" does.
            if (!File.Exists(filePath) && args.Length > 0 && File.Exists("towers.csv"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: File '{filePath}' not found.");
                Console.WriteLine("Falling back to default 'towers.csv'.");
                Console.ResetColor();
                filePath = "towers.csv";
            }
            
            // --- Check if the file (either default or specified) exists ---
            if (!File.Exists(filePath))
            {
                 Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: File not found at '{filePath}'");
                Console.WriteLine("Please ensure 'towers.csv' is in the same directory or provide a valid path.");
                Console.ResetColor();
                return; // Exit the program
            }

            Console.WriteLine($"Loading towers from: {filePath}");
            Console.WriteLine($"Interference Radius: {INTERFERENCE_RADIUS_KM * 1000} meters");
            Console.WriteLine($"Available Frequencies: {string.Join(", ", AvailableFrequencies)}\n");

            try
            {
                // 1. Load the data
                List<CellTower> towers = LoadTowersFromCSV(filePath);
                
                if (towers.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: No towers were loaded. The file might be empty or all lines were corrupt.");
                    Console.ResetColor();
                    return; // Exit
                }

                Console.WriteLine($"Successfully loaded {towers.Count} towers.");

                // 2. Run the allocation algorithm
                Dictionary<string, int> allocations = AllocateFrequencies(towers);

                // 3. Print the final report
                Console.WriteLine("\n--- Final Frequency Allocation Plan ---");
                Console.WriteLine("---------------------------------------");
                Console.WriteLine("| Cell ID | Assigned Frequency |");
                Console.WriteLine("|---------|--------------------|");

                // Print results in a clean table, sorted by Cell ID.
                foreach (var allocation in allocations.OrderBy(kv => kv.Key))
                {
                    Console.WriteLine($"| {allocation.Key,-7} | {allocation.Value,-18} |");
                }
                Console.WriteLine("---------------------------------------");

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// This is the core logic. It builds a graph, sorts by degree, and assigns frequencies.
        /// </summary>
        /// <param name="towers">The list of all cell towers.</param>
        /// <returns>A dictionary mapping Cell ID (string) to its assigned frequency (int).</returns>
        private static Dictionary<string, int> AllocateFrequencies(List<CellTower> towers)
        {
            // --- 1. Build the Adjacency List (The Graph) ---
            // Key: CellID
            // Value: List of CellIDs that are "close" (neighbours)
            var adjacencyList = new Dictionary<string, List<string>>();
            
            // Initialize the dictionary with empty lists for all towers
            foreach (var tower in towers)
            {
                adjacencyList[tower.CellID] = new List<string>();
            }

            // Iterate through every unique pair of towers to find neighbours
            for (int i = 0; i < towers.Count; i++)
            {
                for (int j = i + 1; j < towers.Count; j++)
                {
                    CellTower towerA = towers[i];
                    CellTower towerB = towers[j];

                    double distance = CalculateHaversineDistance(towerA, towerB);

                    // If they are within the interference radius, add an "edge"
                    if (distance < INTERFERENCE_RADIUS_KM)
                    {
                        adjacencyList[towerA.CellID].Add(towerB.CellID);
                        adjacencyList[towerB.CellID].Add(towerA.CellID);
                    }
                }
            }

            // --- 2. Sort Towers by Degree (Welsh-Powell inspired) ---
            // We order towers by their "degree" (number of neighbours) descending.
            // This handles the most constrained nodes first.
            var sortedTowers = towers
                .Select(t => new { Tower = t, Degree = adjacencyList[t.CellID].Count })
                .OrderByDescending(x => x.Degree)
                .Select(x => x.Tower)
                .ToList();

            Console.WriteLine("\n--- Processing Order (Sorted by Degree) ---");
            foreach (var tower in sortedTowers)
            {
                Console.WriteLine($"  - {tower.CellID} (Neighbours: {adjacencyList[tower.CellID].Count})");
            }

            // --- 3. Assign Frequencies (Colouring the Graph) ---
            var assignedFrequencies = new Dictionary<string, int>();
            
            Console.WriteLine("\n--- Allocation Log ---");

            foreach (var towerToAssign in sortedTowers)
            {
                // Find all frequencies already used by this tower's neighbours
                var usedFrequenciesByNeighbours = new HashSet<int>();
                
                // Get all neighbours for the current tower
                List<string> neighbours = adjacencyList[towerToAssign.CellID];

                foreach (string neighbourId in neighbours)
                {
                    // Check if this neighbor has *already* been assigned a frequency
                    if (assignedFrequencies.TryGetValue(neighbourId, out int freq))
                    {
                        usedFrequenciesByNeighbours.Add(freq);
                    }
                }

                // Now, find the first available frequency that is *not* in the used set
                int assignedFreq = 0;
                foreach (int freq in AvailableFrequencies)
                {
                    if (!usedFrequenciesByNeighbours.Contains(freq))
                    {
                        assignedFreq = freq;
                        break; // Found a valid frequency
                    }
                }

                // Handle the case where we run out of frequencies (6 colours aren't enough)
                if (assignedFreq == 0)
                {
                    // This is a "k-colouring" problem. If we get here, it means
                    // the graph requires more than 6 colors (is not 6-colorable).
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"WARNING: Could not assign frequency to {towerToAssign.CellID}.");
                    Console.WriteLine($"  Its neighbours {string.Join(", ", neighbours)} already use all available frequencies.");
                    Console.ResetColor();
                    assignedFrequencies[towerToAssign.CellID] = -1; // -1 indicates failure
                }
                else
                {
                    assignedFrequencies[towerToAssign.CellID] = assignedFreq;
                    Console.WriteLine($"Assigned {assignedFreq} to Cell {towerToAssign.CellID}. (Neighbours used: [{string.Join(", ", usedFrequenciesByNeighbours)}])");
                }
            }

            return assignedFrequencies;
        }

        /// <summary>
        /// Loads a CSV file of tower data.
        /// Skips the header row.
        /// Format: "Cell ID","Easting","Northing","Long","Lat"
        /// </summary>
        /// <param name="filePath">Path to the CSV file.</param>
        /// <returns>A List of CellTower objects.</returns>
        private static List<CellTower> LoadTowersFromCSV(string filePath)
        {
            var towers = new List<CellTower>();
            // ReadAllLines is simple for a small file. For a huge file,
            // I'd use a streaming `StreamReader`.
            var lines = File.ReadAllLines(filePath);

            // Skip(1) to ignore the header row
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // The data is quoted and comma-separated
                string[] parts = line.Split(',')
                                     .Select(p => p.Trim().Trim('"')) // Remove quotes and whitespace
                                     .ToArray();

                if (parts.Length >= 5)
                {
                    try
                    {
                        towers.Add(new CellTower(
                            CellID: parts[0],
                            Latitude: double.Parse(parts[4], CultureInfo.InvariantCulture),
                            Longitude: double.Parse(parts[3], CultureInfo.InvariantCulture)
                        ));
                    }
                    catch (FormatException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Warning: Skipping bad line: {line}. Error: {ex.Message}");
                        Console.ResetColor();
                    }
                }
            }
            return towers;
        }

        /// <summary>
        /// Calculates the distance between two towers using the Haversine formula.
        /// </summary>
        /// <returns>Distance in kilometers.</returns>
        private static double CalculateHaversineDistance(CellTower tower1, CellTower tower2)
        {
            const double earthRadiusKm = 6371.0;

            double lat1 = ToRadians(tower1.Latitude);
            double lon1 = ToRadians(tower1.Longitude);
            double lat2 = ToRadians(tower2.Latitude);
            double lon2 = ToRadians(tower2.Longitude);

            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = earthRadiusKm * c;

            return distance;
        }

        /// <summary>
        /// Helper function to convert degrees to radians.
        /// </summary>
        private static double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}
