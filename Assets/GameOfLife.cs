using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class GameOfLifeGPU : MonoBehaviour
{
    public int rows = 100;
    public int cols = 100;
    public float cellSize = 0.1f;
    public float updateRate = 0.1f;
    
    private int[,] grid;
    private GameObject[,] cells;
    public GameObject cellPrefab;
    public Button startButton, pauseButton, resetButton, toggleModeButton;
    private bool isRunning = false;
    private bool useGPU = false;
    private Stopwatch stopwatch = new Stopwatch();
    public ComputeShader computeShader;
    private ComputeBuffer gridBuffer;
    private ComputeBuffer newGridBuffer;

    void Start()
    {
        grid = new int[rows, cols];
        cells = new GameObject[rows, cols];
        InitializeGrid();
        startButton.onClick.AddListener(StartGame);
        pauseButton.onClick.AddListener(PauseGame);
        resetButton.onClick.AddListener(ResetGame);
        toggleModeButton.onClick.AddListener(ToggleMode);
    }

    void InitializeGrid()
    {
        float offsetX = (rows * cellSize) / 2f;
        float offsetY = (cols * cellSize) / 2f;

        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < cols; y++)
            {
                Vector2 position = new Vector2(x * cellSize - offsetX, y * cellSize - offsetY);
                cells[x, y] = Instantiate(cellPrefab, position, Quaternion.identity);
                grid[x, y] = 0;
                UpdateCellVisual(x, y);
            }
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int x = Mathf.FloorToInt((worldPos.x + (rows * cellSize) / 2) / cellSize);
            int y = Mathf.FloorToInt((worldPos.y + (cols * cellSize) / 2) / cellSize);

            if (x >= 0 && x < rows && y >= 0 && y < cols)
            {
                grid[x, y] = grid[x, y] == 1 ? 0 : 1;
                UpdateCellVisual(x, y);
            }
        }
    }

    void StartGame()
    {
        if (!isRunning)
        {
            isRunning = true;
            StartCoroutine(UpdateGame());
        }
    }

    void PauseGame()
    {
        isRunning = false;
        StopCoroutine(UpdateGame());
    }

    void ResetGame()
    {
        isRunning = false;
        StopCoroutine(UpdateGame());
        InitializeGrid();
    }

    void ToggleMode()
    {
        useGPU = !useGPU;
    }

    IEnumerator UpdateGame()
    {
        while (isRunning)
        {
            stopwatch.Restart();
            grid = useGPU ? GetNextGenerationGPU() : GetNextGenerationCPU();
            stopwatch.Stop();
            UnityEngine.Debug.Log((useGPU ? "GPU" : "CPU") + " Execution Time: " + stopwatch.ElapsedMilliseconds + " ms");
            UpdateGridVisual();
            yield return new WaitForSeconds(updateRate);
        }
    }

    int[,] GetNextGenerationCPU()
    {
        int[,] newGrid = new int[rows, cols];
        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < cols; y++)
            {
                int aliveNeighbors = CountAliveNeighbors(x, y);
                newGrid[x, y] = (grid[x, y] == 1 && (aliveNeighbors == 2 || aliveNeighbors == 3)) || (grid[x, y] == 0 && aliveNeighbors == 3) ? 1 : 0;
            }
        }
        return newGrid;
    }

    int[,] GetNextGenerationGPU()
    {
        if (computeShader == null)
            return GetNextGenerationCPU();

        int[] gridArray = new int[rows * cols];
        for (int x = 0; x < rows; x++)
            for (int y = 0; y < cols; y++)
                gridArray[x * cols + y] = grid[x, y];

        gridBuffer = new ComputeBuffer(rows * cols, sizeof(int));
        newGridBuffer = new ComputeBuffer(rows * cols, sizeof(int));
        gridBuffer.SetData(gridArray);
        
        computeShader.SetInt("rows", rows);
        computeShader.SetInt("cols", cols);
        computeShader.SetBuffer(0, "grid", gridBuffer);
        computeShader.SetBuffer(0, "newGrid", newGridBuffer);

        computeShader.Dispatch(0, rows / 8, cols / 8, 1);

        newGridBuffer.GetData(gridArray);
        gridBuffer.Release();
        newGridBuffer.Release();

        int[,] finalGrid = new int[rows, cols];
        for (int x = 0; x < rows; x++)
            for (int y = 0; y < cols; y++)
                finalGrid[x, y] = gridArray[x * cols + y];
        
        return finalGrid;
    }

    int CountAliveNeighbors(int x, int y)
    {
        int count = 0;
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                int nx = x + i, ny = y + j;
                if (nx >= 0 && nx < rows && ny >= 0 && ny < cols)
                    count += grid[nx, ny];
            }
        }
        return count;
    }

    void UpdateGridVisual()
    {
        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < cols; y++)
            {
                UpdateCellVisual(x, y);
            }
        }
    }

    void UpdateCellVisual(int x, int y)
    {
        cells[x, y].GetComponent<SpriteRenderer>().color = grid[x, y] == 1 ? Color.black : Color.white;
    }
}
