/*
 * Written by:
 * Henrik
*/

using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public class WaveController : MonoBehaviour
{
	[SerializeField] StudioEventEmitter _WaveStart, _WaveComplete;
	[SerializeField] public GameObject EjectParticles;
	// Level variables
	[SerializeField] private int health;
	public int currentHealth => health;
	public LevelWaveSequence LevelData;
	LevelHandler levelHandler;
	[HideInInspector] public FlagPole_Interactable flagPole;
	private int currentWave;
	public int GetWave => currentWave;
	Transform EnemyParent;
	public Transform EnemyHolder => EnemyParent;
	
	// Individual wave variables
	public List<EnemyBehaviour> enemies = new List<EnemyBehaviour>();
	[SerializeField] bool waveIsInProgress, levelComplete, levelIsEnding;
	public bool waveIsPlaying => waveIsInProgress;
	public bool levelWon => levelComplete;
	private int waveProgress;

	// Timer variables
	private float cooldownTimer = 0;
	private float currentCooldown = 0;
	private int repeatSpawn = -1;
	int EnemySpawnCount;

	// Sound variables
	[SerializeField] StudioEventEmitter[] VOEmitters;
	[SerializeField] bool playedDamageVO;
	[SerializeField] int lastHealth; 
	

	void Start()
	{
		flagPole = GameObject.FindGameObjectWithTag("FlagPole").GetComponent<FlagPole_Interactable>();
		levelHandler = GetComponentInParent<LevelHandler>();
		EnemyParent = GameObject.FindGameObjectWithTag("EnemyHolder").transform;
		GameManager.instance.pathController = LevelData.GetPathController();
		health = (int)(LevelData.lives * GameManager.instance.healthMultiplier);
		if(flagPole)
		{
			flagPole.calculateFlagPositions(LevelData.waves.Length);
			flagPole.setFlagPos(currentWave);
		}

		lastHealth = health;
	}

	// Update is called once per frame
	void  Update()
	{
		if(levelComplete && !levelIsEnding) // win condition
			endLevel();

		if(health <= 0 && !levelIsEnding && !levelComplete) // loose condition
			endLevel(true);

		if (waveIsInProgress)
		{
			if (health != lastHealth && !playedDamageVO)
			{
				VOEmitters[3].Play();
				playedDamageVO = true;
			}
			if (waveProgress == getWaveLength() && enemies.Count == 0)
			{
				waveIsInProgress = false;
				EndOfWave();
			}

			if (cooldownTimer > currentCooldown)
			{
				EnemyBehaviour spawn = SpawnNextEnemy();

				if (spawn)
					enemies.Add(spawn);
			}
			else
				cooldownTimer += Time.deltaTime;
		}
		else
        {
			playedDamageVO = false;
			lastHealth = health;
		}
	}

	public void nextWave()
	{
		if(!levelIsEnding)
		{
			if (!levelComplete)
			{
				_WaveStart.Play();
				waveIsInProgress = true;
				GameManager.instance.setLights(false);
				//Put mines on side of table here.
				MineSpawner.Instance.DestroyAllMines();
				MineSpawner.Instance.InstantiateMine(2);

				VOEmitters[0].Play();
			}
			else
			{
				endLevel();
				MineSpawner.Instance.DestroyAllMines();
			}
		}
	}
	
	public void endLevel(bool lose = false)
	{
		waveIsInProgress = false;

		if (lose)
		{
			Debug.Log("Player Lost");
			// stuff here when player looses

			if (CanvasController.instance.getCurrentGaphic != 1)
				CanvasController.instance.OpenNewCanvas(1);

			GameManager.instance.restorePreLevelTowers();
			CurrencyManager.instance.restorePreLevelMoney();

			currentWave = getWaveCount();

			levelHandler._LooseTriggerMUS.TriggerParameters();
			// Will trigger win stinger upon Win. A 2 bar transition between music and stinger
			// Additional feedback should be delayed by 2 seconds ish to match with stinger

			VOEmitters[1].Play(); // Trigger Loose VO, AKA Defeat VO FMOD Event
		}
		else
		{
			// Debug.Log("Player won");
			GameManager.instance.SaveLevelComplete(LevelData.levelNumber);
			CanvasController.instance.OpenNewCanvas(0);

			levelHandler._WinTriggerMUS.TriggerParameters();
			VOEmitters[2].Play(); // Trigger Victory VO

			for(int i = 0; i< levelHandler.Confetti.Length; i++)
            {
				levelHandler.Confetti[i].Play();
            }
		}

		foreach (var enemy in enemies)
		{
			Destroy(enemy.gameObject);
		}
		enemies = new List<EnemyBehaviour>();
		levelIsEnding = true;


		Debug.Log("Level is complete, Level stats are missing");
	}
	
	public void	dealDamage(int damage)
	{
		health -= damage;
		updateCoveVFX();
	}
	
	public void	heal(int life)
	{
		health += life;
		updateCoveVFX();
	}

	private void updateCoveVFX()
	{
		int fireCount = (int)((float)(LevelData.lives-health) / ((float)LevelData.lives / (float)levelHandler.fireParticles.Length));
		for (int i = 0; i < fireCount; i++)
		{
			levelHandler.fireParticles[i].SetActive(true);
		}
	}
	
	private int getWaveLength()
	{
		return LevelData.waves[currentWave].waveData.Length;
	}
	
	public int getWaveCount()
	{
		return LevelData.waves.Length;
	}
	
	private void EndOfWave()
	{
		GameManager.instance.setLights(true);
		_WaveComplete.Play();
		// Debug.Log(currentWave);
		currentWave++;
		waveProgress = 0;
		cooldownTimer = 0;
		currentCooldown = 0;
		repeatSpawn = -1;
		
		if(flagPole)
			flagPole.setFlagPos(currentWave);

		if(currentWave == getWaveCount())
			levelComplete = true;
	}
	
	EnemyBehaviour SpawnNextEnemy()
	{
		GameObject spawn = null;
		
		// Debug.Log("wave progress");
		if(repeatSpawn == -1 && getLevelState())
		{
			repeatSpawn = getRepeatSpawn();
		}
		else if(repeatSpawn > -1)
		{
			// update repeat spawning
			repeatSpawn--;
			
			// Spawn the next enemy
			spawn = Instantiate(getEnemyPrefab());
			EnemySpawnCount++;
			spawn.name += " " + EnemySpawnCount;

			spawn.transform.SetParent(EnemyParent, true);
			
			currentCooldown = getCooldown();
			cooldownTimer = 0;
			
			// update wave progress		
			if(repeatSpawn == -1)
				waveProgress++;
			// Debug.Log("decrease spawn repeat");
		}
		if(spawn != null)
			return spawn.GetComponent<EnemyBehaviour>();
		// Debug.Log(" no enemy was spawned");
		return null;
	}
	
	public bool getLevelState()
	{
		if(LevelData.waves[currentWave].waveData.Length > waveProgress)
			return true;
		return false; 
	}
	
	GameObject getEnemyPrefab()
	{
		return LevelData.waves[currentWave].waveData[waveProgress].enemyPfb;
	}
	
	private float getCooldown()
	{
		return LevelData.waves[currentWave].waveData[waveProgress].Cooldown;
	}
	
	private int getRepeatSpawn()
	{
		// Debug.Log("wave: "+currentWave + " Progress: " + waveProgress);
		// Debug.Log("wave Length: "+LevelData.waves.Length + " Progress: " + LevelData.waves[currentWave].waveData.Length);
		int n = Mathf.Clamp(LevelData.waves[currentWave].waveData[waveProgress].repeat -1, 0, 100000);
		return n;
	}
	
		public Vector3 GetPosOfEnemy(int index)
	{
		return enemies[index].GetPosition();
	}

	public float GetProgressOfEnemy(int index)
	{
		return enemies[index].GetProgress;
	}
	

	public void AddEnemy(EnemyBehaviour e)
	{
		e.path = GameManager.instance.pathController;
		
		enemies.Add(e);
	}

	public void RemoveEnemy(EnemyBehaviour e)
	{
		enemies.Remove(e);
	}
}