/***************************************************************************************************************************
 * 
 * Created by Fabi Fiess in February 2016 for the Game "GravityHunter"
 * This script enables changing level data and adding new levels without installing  a completely new version
 * On game start this cript checks if a connection to the database is established. This is the case when the player 
 * has a working internet connection. If there is no internetconnection, the player gets the level data from the 
 * levels.json file. If there IS an internet connection available, the app compares the database data and the .json data. 
 * If the database has newer or more entries than the .json file, the database data will be stored into the levels.json file, 
 * before the app loads from the levels.json when initializing the level
 * 
 ****************************************************************************************************************************/

using UnityEngine;
using System; //
using System.Collections;
using System.Collections.Generic;  // Lists
// MongoDB
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;  
using MongoDB.Driver.GridFS;  
using MongoDB.Driver.Linq;  
// JsonUtilities
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
// Check internet connection
using System.Net;


public class Sync : MonoBehaviour {
	
	public string jsonFilePath;
	public string mongoConnectionString;
	public MongoDatabase database;
	public MongoCollection<BsonDocument> levelsCollection;
	public bool isDbConnected;

	void Start () {



		/****************************************************************
		 * Config: Database connection and file path
		 ****************************************************************/



		jsonFilePath = Application.persistentDataPath + "/levels.json";
		//mongoConnectionString = "mongodb://localhost:27017";                               // localhost
		mongoConnectionString = "mongodb://gravityhunter.mi.hdm-stuttgart.de:27017";     // remote server
		Debug.Log ("File datapath is: "+ jsonFilePath);

		// Check if internet connection is available by calling HasInternetConnection();





		/****************************************************************
		 * DB:
		 * Establish connection to mongodb
		 ****************************************************************/



	

		try{ 
			// try to establish database connection

			var client = new MongoClient(mongoConnectionString);
			var server = client.GetServer(); 
			database = server.GetDatabase("test");

			// remove this line in production, otherwise database collection will be removed on every start.
			database.GetCollection<BsonDocument>("levels").RemoveAll();  // remove this line in production, otherwise database collection will be removed on every start.

			levelsCollection= database.GetCollection<BsonDocument>("levels");
			isDbConnected = true;
			Debug.Log ("Established connection to mongodb");
		}

		// if there is no database connection

		catch{
			isDbConnected = false;
			Debug.Log ("No connection to MongoDB available");
		}







		if(isDbConnected){
			
			// Load init data into DB and levels.json 
			loadInitDataIntoDb ();
			loadInitDataIntoJsonFile ();

			// get data from DB and levels.json
			List<BsonDocument> dbData = getDbData ();
			MainDataObject jsonData = getJsonData ();

			List<DateTime> versions_json = getJsonTimes (jsonData);

			/*
			 * Compare the version timestamp of every array entry. 
			 * If database contains an updated version, store the new data in .json file
			 * Besides the 2 objects jsonData and dbData create the object syncedData
			 * create bool variable areDifferent which becomes false if dbData contains newer or additional objects
			 * if diffData is truesave the syncedData file into the levels.json file  
			 */

			bool isAmountDifferent = checkIfDifferentNumbersOfEntries(dbData, jsonData);
			bool isDbNewer = checkIfDbContainsNewerDocuments(dbData, versions_json);

			// if db is more up to date, put all db data into levels.json

			if (isAmountDifferent == true || isDbNewer == true) putDbDataIntoJsonFile (dbData);


		


			/*
			 * Load level.json (in this case it's the new one)
			 */



		}


		if (!isDbConnected) {

			/*
			 * Load init data into levels.json
			 */

			loadInitDataIntoJsonFile ();

		}
	
	}







	public List<BsonDocument> getDbData(){
		// DB: Get all documents from levels collection and store them in list
		var dbData = new List<BsonDocument> ();
		foreach (var document in levelsCollection.FindAll()) {
			dbData.Add (document);
		}

		foreach (var entry in dbData) {
			Debug.Log ("DB entry: " + entry);
		}
		// logs
		//  { "_id" : ObjectId("56d356352a1e030aa5b3daaf"), "id" : 1, "version" : ISODate("2016-02-27T00:00:00Z"), "name" : "Level 1", "controls" : "#DE4526", "area" : "Gruene Wiese" }
		//  { "_id" : ObjectId("56d356352a1e030aa5b3dab0"), "id" : 2, "version" : ISODate("2015-02-23T00:00:00Z"), "name" : "Level 2", "controls" : "#DE4526", "area" : "Wald" }

		return dbData;
	}






	public MainDataObject getJsonData(){
		// Get all entries from levels.json
		string jsonFileString = File.ReadAllText (jsonFilePath);
		return JsonUtility.FromJson<MainDataObject> (jsonFileString);
	}





	public List<DateTime> getJsonTimes (MainDataObject jsonData){
		var versions_json = new List<DateTime> ();
		for (int i=0; i < jsonData.levels.Length; i++) {
			// it's not possible to put whole documents in array
			// Convert all date strings in JSON file to date object
			DateTime dateValue;
			DateTime.TryParse (jsonData.levels[i].version, out dateValue);
			versions_json.Add (dateValue);
		}

		foreach (var entry in versions_json) {
			Debug.Log ("versions_json: " + entry);  // DataTime Objects!!
		}

		// logs
		// 02/28/2016 22:23:35
		// 01/28/2016 22:23:35

		return versions_json;
	}




	bool checkIfDifferentNumbersOfEntries (List<BsonDocument> dbData, MainDataObject jsonData){
		// does the number of objects in .json file equal the number of objects in db?
		if (jsonData.levels.Length != dbData.Count)return true;
		else return false;
		//Debug.Log ("jsonData.levels.Length: " + jsonData.levels.Length);
		//Debug.Log ("dbData.Count: " + dbData.Count);
	}




	bool checkIfDbContainsNewerDocuments(List<BsonDocument> dbData, List<DateTime> versions_json){
		// compare the DataTime in db and levels.json
		bool isDbNewer = false;
		for (int i = 0; i < versions_json.Count; i++) {
			var dbEntry = dbData [i] ["version"];   // BsonValue (enthält ein DateTime-Object)
			var jsonEntry = versions_json [i];      // DateTime-Objekt (wurde von einem string umgewandelt)

			// Debug.Log ("dbEntry: "+ dbEntry);
			// Debug.Log ("jsonEntry: "+ jsonEntry);

			if (dbEntry > jsonEntry) {
				Debug.Log ("DB ist aktueller bei Eintrag " + i);
				isDbNewer = true;
				break;
			}
		}
		return isDbNewer;
	}



	public void putDbDataIntoJsonFile(List<BsonDocument> dbData){
		// if db is more up to date, put all db data into levels.json
	
		Debug.Log ("Write db collection into levels.json because db is more up to date.");

		/*
			 * Insert level data into .json file
			 */


		// InnerObject data

		var syncedInnerObjectList = new List<InnerObjectData> ();

		for (int i = 0; i < dbData.Count; i++) {
			syncedInnerObjectList.Add(
				createInnerObject(
					dbData [i] ["id"].ToInt32(),                      
					toJsonDate(dbData [i] ["version"].ToString()),      // string version (not possible to store date object, sorry!
					dbData [i] ["name"].ToString(),                    
					dbData [i] ["controls"].ToString(),                
					dbData [i] ["area"].ToString(),                    
					dbData [i] ["movesMax"].ToInt32(),                      
					dbData [i] ["moves1"].ToInt32(),                       
					dbData [i] ["moves2"].ToInt32(),                       
					dbData [i] ["moves3"].ToInt32(),                     
					toStringArray(dbData [i] ["gameField"]),                       
					toStringArray(dbData [i] ["gameFieldNegative"])
				)
			);
		}


		// Put InnerObjectDataList into MainDataArray

		var syncedMainDataObject = new MainDataObject ();
		syncedMainDataObject.levels = syncedInnerObjectList.ToArray ();

		string syncedJsonString = JsonUtility.ToJson(syncedMainDataObject);
		Debug.Log ("generated synced JsonString: " + syncedJsonString);
		Debug.Log ("persistentDataPath: " + Application.persistentDataPath);
		File.WriteAllText(jsonFilePath, syncedJsonString);

	}






	public void loadInitDataIntoDb(){

		/****************************************************************
		 * Only for developing or for inserting init data to db
		 * DB: 
		 * Insert level data into levels collection in mongodb
		 ****************************************************************/


		BsonDocument [] batch={
			new BsonDocument {     // level_1.json
				{ "id", 0 },
				{ "version", new DateTime(2016,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 1" },
				{ "controls", "#0B8611" },
				{ "area", "Grüne Wiese" },
				{ "movesMax", 10 },
				{ "moves1", 10 },
				{ "moves2", 5 },
				{ "moves3", 3 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,1,0,0,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", new BsonArray{
						{"0,-1,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_2.json
				{ "id", 1 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 2" },
				{ "controls", "#0B8611" },
				{ "area", "Grüne Wiese" },
				{ "movesMax", 12 },
				{ "moves1", 12 },
				{ "moves2", 5 },
				{ "moves3", 3 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,1,0,0,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,-2"},
						{"0,-1,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_3.json
				{ "id", 2 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 3" },
				{ "controls", "#0B8611" },
				{ "area", "Grüne Wiese" },
				{ "movesMax", 13 },
				{ "moves1", 13 },
				{ "moves2", 9 },
				{ "moves3", 7 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,1,2,0,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,-2"},
						{"0,-1,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_4.json
				{ "id", 3 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 4" },
				{ "controls", "#0B8611" },
				{ "area", "Grüne Wiese" },
				{ "movesMax", 13 },
				{ "moves1", 13 },
				{ "moves2", 9 },
				{ "moves3", 7 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,1,0,0,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,-2,0,0,0,0,0"},
						{"0,0,0,0,-2,-1,0,0"},
						{"0,-2,0,0,0,0,0,-2"},
						{"0,0,0,0,0,0,0,0"},
						{"0,-2,0,0,0,0,0,0"},
						{"0,0,-2,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,-2,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_5.json
				{ "id", 4 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 5" },
				{ "controls", "#0B8611" },
				{ "area", "Grüne Wiese" },
				{ "movesMax", 15 },
				{ "moves1", 15 },
				{ "moves2", 9 },
				{ "moves3", 6 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"4,0,0,0,0,0,0,0"},
						{"4,4,0,0,2,0,3,3"},
						{"4,4,0,0,2,0,0,0"},
						{"0,1,0,0,2,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,-2,0,0"},
						{"0,0,0,0,0,-2,0,0"},
						{"0,0,0,0,0,-2,0,0"},
						{"0,0,-2,0,0,-2,0,0"},
						{"0,0,0,0,0,-2,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,-2,0,0"},
						{"0,0,0,0,0,-2,0,-1"}
					} 
				}
			},

			new BsonDocument {     // level_6.json
				{ "id", 5 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 6" },
				{ "controls", "#D065785" },
				{ "area", "Meadow" },
				{ "movesMax", 7 },
				{ "moves1", 17 },
				{ "moves2", 5 },
				{ "moves3", 4 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,4"},
						{"0,0,0,0,0,0,0,4"},
						{"0,0,0,0,0,0,0,4"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,2"},
						{"0,0,0,0,3,3,0,1"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,-2,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,-2,-2,-2,-2,-2"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"-1,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_7.json
				{ "id", 6 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 7" },
				{ "controls", "#D065785" },
				{ "area", "Meadow" },
				{ "movesMax", 15 },
				{ "moves1", 15 },
				{ "moves2", 12 },
				{ "moves3", 11 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,1,0,0,0"},
						{"0,0,0,0,1,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,3,3,0,0"},
						{"0,0,0,0,0,2,0,0"},
						{"0,0,0,0,0,2,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,-2,0"},
						{"0,0,0,0,0,0,-2,0"},
						{"0,0,-2,-2,-2,0,-2,0"},
						{"0,0,-2,-2,-2,0,-2,0"},
						{"0,0,-2,0,0,0,0,-1"},
						{"0,0,-2,0,0,0,0,-1"},
						{"0,0,0,0,0,0,-2,-2"},
						{"0,0,0,0,0,0,-2,-2"}
					} 
				}
			},

			new BsonDocument {     // level_8.json
				{ "id", 7 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 8" },
				{ "controls", "#D065785" },
				{ "area", "Meadow" },
				{ "movesMax", 15 },
				{ "moves1", 15 },
				{ "moves2", 10 },
				{ "moves3", 7 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,3,3,0,0,1"},
						{"0,0,0,3,3,2,2,1"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,-2,-2,-2,-2,0,0,0"},
						{"0,0,0,-1,0,0,0,-2"},
						{"0,0,0,-1,0,0,0,-2"},
						{"0,0,-2,-2,-2,-2,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_9.json
				{ "id", 8 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 9" },
				{ "controls", "#D42D07" },
				{ "area", "LavaCave" },
				{ "movesMax", 10 },
				{ "moves1", 10 },
				{ "moves2", 6 },
				{ "moves3", 4 },
				{ "gameField", 
					new BsonArray{
						{"2,2,0,0,0,0,0,0"},
						{"2,2,0,0,0,0,0,0"},
						{"2,2,0,0,0,0,0,0"},
						{"2,2,0,0,0,0,0,0"},
						{"2,2,0,0,0,0,0,0"},
						{"3,3,1,0,0,0,0,0"},
						{"4,0,5,5,0,0,0,0"},
						{"4,4,5,5,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,-1,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_10.json
				{ "id", 9 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 10" },
				{ "controls", "#D42D07" },
				{ "area", "LavaCave" },
				{ "movesMax", 18 },
				{ "moves1", 18 },
				{ "moves2", 10 },
				{ "moves3", 8 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"1,1,2,2,0,0,0,0"},
						{"1,0,2,2,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,-2,0,0,0,0,-2,-2"},
						{"-1,-1,0,0,0,0,0,0"},
						{"-1,0,0,0,0,0,0,0"},
						{"-2,-2,0,0,0,0,0,0"},
						{"0,0,0,0,-2,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_11.json
				{ "id", 10 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 11" },
				{ "controls", "#D42D07" },
				{ "area", "LavaCave" },
				{ "movesMax", 12 },
				{ "moves1", 12 },
				{ "moves2", 9 },
				{ "moves3", 7 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,3,3,0,0"},
						{"0,0,0,0,3,3,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,2,2,0"},
						{"0,0,0,0,0,0,1,0"},
						{"0,0,0,0,0,0,1,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"-2,-2,-2,-1,-2,-2,-2,-2"},
						{"-2,0,0,-1,0,0,-2,-2"},
						{"-2,0,0,0,0,0,-2,-2"},
						{"-2,-2,-2,-2,-2,0,0,-2"},
						{"0,0,0,0,0,0,0,-2"},
						{"-2,-2,0,0,0,0,0,-2"},
						{"-2,-2,0,0,0,0,0,-2"},
						{"-2,-2,-2,-2,-2,-2,-2,-2"}
					} 
				}
			},

			new BsonDocument {     // level_12.json
				{ "id", 12 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 2" },
				{ "controls", "#D42D07" },
				{ "area", "LavaCave" },
				{ "movesMax", 23 },
				{ "moves1", 23 },
				{ "moves2", 15 },
				{ "moves3", 12 },
				{ "gameField", 
					new BsonArray{
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"2,0,3,0,0,0,0,0"},
						{"2,0,3,1,0,0,0,4"},
						{"2,0,3,1,0,0,0,4"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,-2,0,0,0,0,0,0"},
						{"0,-2,0,0,0,0,0,0"},
						{"0,-2,0,-2,0,0,0,0"},
						{"-1,-2,-2,-2,0,-2,-2,-2"},
						{"-1,-2,0,0,0,-2,0,0"},
						{"0,-2,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"}
					} 
				}
			},

			new BsonDocument {     // level_13.json
				{ "id", 12 },
				{ "version", new DateTime(2012,2,25, 0,0,0, DateTimeKind.Utc) },
				{ "name", "Level 13" },
				{ "controls", "#D42D07" },
				{ "area", "LavaCave" },
				{ "movesMax", 38 },
				{ "moves1", 38 },
				{ "moves2", 32 },
				{ "moves3", 30 },
				{ "gameField", 
					new BsonArray{
						{"1,0,0,0,0,0,0,0"},
						{"2,0,0,0,0,0,0,0"},
						{"2,3,3,0,0,0,0,0"},
						{"0,0,0,0,0,5,0,0"},
						{"0,0,0,0,0,5,4,4"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,7,6,6,0"}
					} 
				},
				{ "gameFieldNegative", 
					new BsonArray{
						{"0,0,0,0,0,0,0,-2"},
						{"0,0,0,0,0,0,0,-2"},
						{"0,0,0,0,0,0,0,0"},
						{"-2,-2,-2,-2,0,0,0,0"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,0,-2,-2,-2,-2"},
						{"0,0,0,0,0,0,0,0"},
						{"0,0,0,-2,0,0,0,-1"}
					} 
				}
			}











		};

		levelsCollection.InsertBatch (batch);
		// Debug.Log ("DB: Inserted level data into levels collection in mongodb");


	}








	public void loadInitDataIntoJsonFile(){

		/****************************************************************
		 * Only for developing or for inserting init data into levels.json
		 * .JSON: 
		 * Insert level data into levels.json file
		 ****************************************************************/


		/*
		* Create inner object data and put them into a list
		*/

		var innerObjectList = new List<InnerObjectData> ();

		innerObjectList.Add(
			createInnerObject(
				1,                          // int id
				"02/28/2016 22:23:35",      // string version (not possible to store date object, sorry!
				"Aus levels.json",          // string name
				"#DE4526",                  // string controls
				"Gruene Wiese",             // string area
				20,                         // int movesMax
				15,                         // int moves1
				12,                         // int moves2
				10,                         // int moves3
				new string[8] {             // string[] gameField
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0"
				},                          
				new string[8] {             // string[] gameFieldNegative
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0"
				}                           
			)
		);


		innerObjectList.Add(
			createInnerObject(
				1,                          // int id
				"04/23/2013 22:23:35",      // string version (not possible to store date object, sorry!
				"Aus levels.json",          // string name
				"#DE4526",                  // string controls
				"Gruene Wiese",             // string area
				20,                         // int movesMax
				15,                         // int moves1
				12,                         // int moves2
				10,                         // int moves3
				new string[8] {             // string[] gameField
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0"
				},                          
				new string[8] {             // string[] gameFieldNegative
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0",
					"2,2,0,0,0,0,0,0"
				}
			)
		);



		/*
		 *  Put InnerObjectDataList into MainDataArray
		 */

		var mainDataObject = new MainDataObject ();
		mainDataObject.levels = innerObjectList.ToArray ();


		string jsonString = JsonUtility.ToJson(mainDataObject);
		Debug.Log ("generated JsonString: " + jsonString);
		Debug.Log (Application.persistentDataPath);
		// if Unity breaks, it's because of the subfolder "files", create it yourself. On Android give the permission to make this done automatically.
		// go to Files/Build Settings/Player Settings... In Inspektor: Select right tab Android, go to Other settings... Write Access: External (SD Card)
		File.WriteAllText(jsonFilePath, jsonString);


		Debug.Log (".JOSON: Inserted level data into .json file");


	}


	public static bool HasInternetConnection(){
		try{
			using (var client = new WebClient())
			using (var stream = new WebClient().OpenRead("http://gravityhunter.mi.hdm-stuttgart.de")){
				return true;
			}
		}catch{
			return false;
		}
	}

	public InnerObjectData createInnerObject(int id, string version, string name, string controls, string area, int movesMax, int moves1, int moves2, int moves3, string [] gameField, string [] gameFieldNegative){
		var myInnerObject = new  InnerObjectData();

		myInnerObject.id = id;
		myInnerObject.version = version;
		myInnerObject.name = name;
		myInnerObject.controls = controls;
		myInnerObject.area = area;
		myInnerObject.movesMax = movesMax;
		myInnerObject.moves1 = moves1;
		myInnerObject.moves2 = moves2;
		myInnerObject.moves3 = moves3;
		myInnerObject.gameField = gameField;
		myInnerObject.gameFieldNegative = gameFieldNegative;

		return myInnerObject;
	}


	public string toJsonDate(String fromDB){         // in: 2016-02-29T00:00:00Z	 
		DateTime dateValue;
		DateTime.TryParse (fromDB, out dateValue);   // out: 02/29/2016 01:00:00
		return dateValue.ToString();
	}

	public string[] toStringArray(BsonValue arrayFromDB){
		string [] stringArray = new String[8];
		for (int i = 0; i < 8; i++) {
			stringArray [i] = arrayFromDB [0].ToString ();
		}
		return stringArray;
	}

}

// for decoding levels.json

[Serializable]
public class InnerObjectData {
	public int id;
	public string version;
	public string name;
	public string controls;
	public string area;
	public int movesMax;
	public int moves1;
	public int moves2;
	public int moves3;
	public string [] gameField;
	public string [] gameFieldNegative;
}

[Serializable]
public class MainDataObject {
	public InnerObjectData[] levels;
}
		