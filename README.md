# Cellphone-Frequencies-Allocation
C# console app for solving cell tower frequency allocation using a graph coloring algorithm. The purpose of this to allocate frequencies in the most efficient manner such that the cells farthest to another cell can have the same frequency in order to reduce interference.

This solution loads cell tower data from a .csv file, models the towers and their interference zones as a graph, and assigns frequencies from a limited set (110-115) to ensure no two "close" towers share the same frequency.

---

## Prerequisites
To build and run this project, you will need:
* .NET SDK installed on your machine 
* Git 

**Steps to run from your terminal:**

1.  **Clone the repository:**
    ```sh
    git clone <your-repository-url>
    cd CellFrequencyAllocation
    ```

2.  **Execute the project**
    ```sh
    dotnet run
    ```
    Or if you have a custom csv file:
    ```sh
    dotnet run path/to/your/custom_towers.csv
    ```

## Note on CSV Format 
The program expects the CSV file to have a header row and for the data to be in the following format. Decimal numbers must use a period (```.```).
```sh
"Cell ID","Easting","Northing","Long","Lat"
"A","536660","183800","-0.03098","51.53657"
...
```
