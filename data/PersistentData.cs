using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

class PersistentData {
	/*
	Saves and loads data in a simpler fashion
	* Uses System I/O when possible, or PlayerPrefs in web version
	* Uses singleton access, no instantiation or references necessary

	* Advantages to PlayerPrefs:
	* Allow defaults
	* Also save/load bools, double, long
	* No collision of data when using persistent data instances
	* faster?

	How to:
	PersistentData pd = PersistentData.getInstance();
 
	// Read/write:
	pd.SetString("name", "dude");
	var str = pd.GetString("name");

	// Options:
	pd.cacheValues = true; // Save primitive values in memory for faster access

	
	// Good example of another solution (that tries to replace playerprefs): http://www.previewlabs.com/wp-content/uploads/2014/04/PlayerPrefs.cs
	
	TODO:
	* Save/write byte array
	* Save/write serializable object
	* More secure/encrypted writing (use StringUtils.encodeRC4 ?)
	* Save all custom objects to a single file (so it's faster)
	* Save/load lists? test
	* First read may be too slow. Test
	* save/load serializable objects
	* add date time objects?
	* Use SystemInfo.deviceUniqueIdentifier.Substring for key? would not allow carrying over though
	* Make sure files are deleted on key removal/clear
	* Use compression? System.IO.Compression.GZipStream
	* It is NOT safe when executing DeleteAll on web: will delete *everything* from PlayerPrefs (use hashmap?)
	*/

	// Constant properties
	private static Dictionary<string, PersistentData> dataGroups;
	private const string SERIALIZATION_SEPARATOR = ",";
	private const string FIELD_NAME_KEYS = "keys";
	private const string FIELD_NAME_VALUES = "values";
	private const string FIELD_NAME_TYPES = "types";
	
	private const string FIELD_TYPE_BOOLEAN = "b";
	private const string FIELD_TYPE_FLOAT = "f";
	private const string FIELD_TYPE_DOUBLE = "d";
	private const string FIELD_TYPE_INT = "i";
	private const string FIELD_TYPE_LONG = "l";
	private const string FIELD_TYPE_STRING = "s";
	//private static const string FIELD_TYPE_OBJECT = "o"; // Non-primitive

	// Properties
	private string namePrefix;						// Unique prefix for field names
	private string _name;							// Instance name

	private bool _cacheData;						// Whether primitives (ints, floats, etc) should be cached
	//private bool cacheObjects;					// Whether serializable objects should be cached

	private Hashtable dataToBeWritten;				// Data that is waiting to be written: ints, floats, strings, and objects as serialized strings; key, value
	//private List<string> dataToBeDeleted;			// Data that is waiting to be deleted from the system
	private Hashtable cachedData;					// All items that have been cached

	private List<string> dataKeys;					// Keys of all ids used


	// ================================================================================================================
	// STATIC ---------------------------------------------------------------------------------------------------------

	static PersistentData() {
		dataGroups = new Dictionary<string, PersistentData>();
	}
	

	// ================================================================================================================
	// CONSTRUCTOR ----------------------------------------------------------------------------------------------------

	public PersistentData(string name) {
		_name = name;
		namePrefix = getMD5("p_" + _name) + "_";
		_cacheData = true;
		//cacheObjects = false;
		dataToBeWritten = new Hashtable();
		//dataToBeDeleted = new List<String>();
		cachedData = new Hashtable();
		PersistentData.addInstance(this);

		dataKeys = loadStringList(FIELD_NAME_KEYS);

		//Debug.Log("PD keys ==> (" + dataKeys.Count + ") " + dataKeys);
		//foreach (var item in dataKeys) Debug.Log("     [" + item + "]");
	}


	// ================================================================================================================
	// STATIC INTERFACE -----------------------------------------------------------------------------------------------

	private static void addInstance(PersistentData dataGroup) {
		dataGroups.Add(dataGroup.name, dataGroup);
	}

	public static PersistentData getInstance(string name = "") {
		if (dataGroups.ContainsKey(name)) return dataGroups[name];

		// Doesn't exist, create a new one and return it
		return new PersistentData(name);
	}


	// ================================================================================================================
	// PUBLIC INTERFACE -----------------------------------------------------------------------------------------------

	// Get

	public bool GetBool(string key, bool defaultValue = false) {
		return dataKeys.Contains(key) ? getValue<bool>(key) : defaultValue;
	}

	public int GetInt(string key, int defaultValue = 0) {
		return dataKeys.Contains(key) ? getValue<int>(key) : defaultValue;
	}

	public long GetLong(string key, long defaultValue = 0) {
		return dataKeys.Contains(key) ? getValue<long>(key) : defaultValue;
	}

	public float GetFloat(string key, float defaultValue = 0.0f) {
		return dataKeys.Contains(key) ? getValue<float>(key) : defaultValue;
	}

	public double GetDouble(string key, double defaultValue = 0.0) {
		return dataKeys.Contains(key) ? getValue<double>(key) : defaultValue;
	}

	public string GetString(string key, string defaultValue = "") {
		return dataKeys.Contains(key) ? getValue<string>(key) : defaultValue;
	}

	/*
	public T GetObject<T>(string key, T defaultValue = default(T)) {
		return dataKeys.Contains(key) ? deserializeObject(getValue<object>(key)) : defaultValue;
	}
	 */
	
	// Set

	public void SetBool(string key, bool value) {
		dataToBeWritten[key] = value;
		if (_cacheData) cachedData.Remove(key);
	}

	public void SetInt(string key, int value) {
		dataToBeWritten[key] = value;
		if (_cacheData) cachedData.Remove(key);
	}

	public void SetLong(string key, long value) {
		dataToBeWritten[key] = value;
		if (_cacheData) cachedData.Remove(key);
	}

	public void SetFloat(string key, float value) {
		dataToBeWritten[key] = value;
		if (_cacheData) cachedData.Remove(key);
	}

	public void SetDouble(string key, double value) {
		dataToBeWritten[key] = value;
		if (_cacheData) cachedData.Remove(key);
	}

	public void SetString(string key, string value) {
		dataToBeWritten[key] = value;
		if (_cacheData) cachedData.Remove(key);
	}

	/*
	public void SetObject(string key, Object serializableObject) {
		data[key] = serializeObject(serializableObject);
		isDirty = true;
	}
	 * */
	
	// Utils

	public void Clear() {
		dataKeys.Clear();
		dataToBeWritten.Clear();
		//dataToBeDeleted.Clear();
		ClearCache();
		Save(true);
	}

	public void ClearCache() {
		cachedData.Clear();
	}

	public void RemoveKey(string key) {
		dataKeys.Remove(key);
		dataToBeWritten.Remove(key);
		cachedData.Remove(key);
		//dataToBeDeleted.Add(key);
	}

	public bool HasKey(string key) {
		return dataKeys.Contains(key) || dataToBeWritten.Contains(key);
	}
	
	public void Save(bool forced = false) {
		if (dataToBeWritten.Count > 0 || forced) {
			// Some fields need to be saved

			// Read all existing values
			List<string> dataValues = loadStringList(FIELD_NAME_VALUES);
			List<string> dataTypes = loadStringList(FIELD_NAME_TYPES);

			// Record new values
			string fieldKey;
			object fieldValue;
			string fieldType;
			int pos;

			IDictionaryEnumerator enumerator = dataToBeWritten.GetEnumerator();
			while (enumerator.MoveNext()) {
				fieldKey = enumerator.Key.ToString();
				fieldValue = enumerator.Value;
				fieldType = getFieldType(fieldValue);

				if (dataKeys.Contains(fieldKey)) {
					// Replacing a key
					pos = dataKeys.IndexOf(fieldKey);
					dataValues[pos] = Convert.ToString(fieldValue);
					dataTypes[pos] = fieldType;
				} else {
					// Adding a key
					dataKeys.Add(fieldKey);
					dataValues.Add(Convert.ToString(fieldValue));
					dataTypes.Add(fieldType);
				}

				// TODO: remove dataToBeDeleted if necessary (objects?)
			}

			// Finally, write everything
			StringBuilder builderFieldKeys = new StringBuilder();
			StringBuilder builderFieldValues = new StringBuilder();
			StringBuilder builderFieldTypes = new StringBuilder();

			//Debug.Log("SAVING => " + dataKeys.Count + " items");

			for (int i = 0; i < dataKeys.Count; i++) {
				if (i > 0) {
					builderFieldKeys.Append(SERIALIZATION_SEPARATOR);
					builderFieldValues.Append(SERIALIZATION_SEPARATOR);
					builderFieldTypes.Append(SERIALIZATION_SEPARATOR);
				}
				builderFieldKeys.Append(encodeString(dataKeys[i]));
				builderFieldValues.Append(encodeString(dataValues[i]));
				builderFieldTypes.Append(encodeString(dataTypes[i]));
			}

			setSavedString(FIELD_NAME_KEYS, builderFieldKeys.ToString());
			setSavedString(FIELD_NAME_VALUES, builderFieldValues.ToString());
			setSavedString(FIELD_NAME_TYPES, builderFieldTypes.ToString());

			dataToBeWritten.Clear();
			//dataToBeDeleted.Clear();
		}
	}


	// ================================================================================================================
	// ACCESSOR INTERFACE ---------------------------------------------------------------------------------------------

	public string name {
		get { return _name; }
	}

	public bool cacheData {
		get { return _cacheData; }
		set {
			if (_cacheData != value) {
				_cacheData = value;
				if (!_cacheData) ClearCache();
			}
		}
	}


	// ================================================================================================================
	// INTERNAL INTERFACE ---------------------------------------------------------------------------------------------

	private List<string> loadStringList(string fieldName) {
		// Loads a string list from a field
		var encodedList = getSavedString(fieldName).Split(SERIALIZATION_SEPARATOR.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
		var decodedList = new List<string>(encodedList.Length);
		foreach (string listItem in encodedList) {
			decodedList.Add(decodeString(listItem));
		}
		//Debug.Log("Reading field [" + fieldName + "]; value as encoded = [" + getSavedString(fieldName) + "] with [" + encodedList.Length + "] items");
		return decodedList;
	}

	/*
	private string loadStringListItem(string fieldName, int pos) {
		// Load just one position from the string list
		var encodedList = getSavedString(fieldName).Split(SERIALIZATION_SEPARATOR.ToCharArray(), pos + 1);
		Debug.Log("Reading field [" + fieldName + "] at position[" + pos + "]; value as encoded = [" + getSavedString(fieldName) + "] with [" + encodedList.Length + "] items");
		return decodeString(encodedList[pos]);
	}
	*/

	private string getKeyForName(string name) {
		// Return a field name that is specific to this instance
		return namePrefix + name.Replace(".", "_").Replace("/", "_").Replace("\\", "_");
	}

	private T getValue<T>(string key) {
		// Returns the value of a given field, cast to the required type

		// If waiting to be saved, return it
		if (dataToBeWritten.ContainsKey(key)) {
			return (T)dataToBeWritten[key];
		}

		// If already cached, return it
		if (_cacheData && cachedData.ContainsKey(key)) {
			return (T)cachedData[key];
		}

		// Read previously saved data
		var pos = dataKeys.IndexOf(key);
		var fieldType = loadStringList(FIELD_NAME_TYPES)[pos];
		T value = (T)getValueAsType(loadStringList(FIELD_NAME_VALUES)[pos], fieldType);

		// Save to cache
		if (_cacheData) cachedData[key] = value;
		/*
		if (fieldType == FIELD_TYPE_OBJECT) {
			// Object
			if (cacheObjects) cachedData[key] = value;
		} else {
			// Primitive
			if (cachePrimitives) cachedData[key] = value;
		}
		 * */

		return value;

		// Not found, use type default (should never happen?)
		//return default(T);
	}


	private string getMD5(string src) {
		// Basic MD5 for short-ish name uniqueness
		// Source: http://wiki.unity3d.com/index.php?title=MD5
		
		System.Text.UTF8Encoding ue = new System.Text.UTF8Encoding();
		byte[] bytes = ue.GetBytes(src);
	 
		// Encrypt
		System.Security.Cryptography.MD5CryptoServiceProvider md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
		byte[] hashBytes = md5.ComputeHash(bytes);
	 
		// Convert the encrypted bytes back to a string (base 16)
		string hashString = "";
	 
		for (int i = 0; i < hashBytes.Length; i++) {
			hashString += System.Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
		}
	 
		return hashString.PadLeft(32, '0');
	}
	
	private string encodeString(string src) {
		return src.Replace("\\", "\\\\").Replace(SERIALIZATION_SEPARATOR, "\\" + SERIALIZATION_SEPARATOR);
	}

	private string decodeString(string src) {
		return src.Replace("\\" + SERIALIZATION_SEPARATOR, SERIALIZATION_SEPARATOR).Replace("\\\\", "\\");
	}
	
	private object getValueAsType(string fieldValue, string fieldType) {
		if (fieldType == FIELD_TYPE_BOOLEAN)	return Convert.ToBoolean(fieldValue);
		if (fieldType == FIELD_TYPE_INT)		return Convert.ToInt32(fieldValue);
		if (fieldType == FIELD_TYPE_LONG)		return Convert.ToInt64(fieldValue);
		if (fieldType == FIELD_TYPE_FLOAT)		return Convert.ToSingle(fieldValue);
		if (fieldType == FIELD_TYPE_DOUBLE)		return Convert.ToDouble(fieldValue);
		if (fieldType == FIELD_TYPE_STRING)		return (object)fieldValue.ToString();
		//if (fieldType == FIELD_TYPE_OBJECT)		return (object)fieldValue.ToString();

		Debug.LogError("Unsupported type for conversion: [" + fieldType + "]");
		return null;
	}

	private string getFieldType(object value) {
		var realFieldType = value.GetType().ToString();
		// TODO: use "is" ? 
		if (realFieldType == "System.Boolean")	return FIELD_TYPE_BOOLEAN;
		if (realFieldType == "System.Int32")	return FIELD_TYPE_INT;
		if (realFieldType == "System.Int64")	return FIELD_TYPE_LONG;
		if (realFieldType == "System.Single")	return FIELD_TYPE_FLOAT;
		if (realFieldType == "System.Double")	return FIELD_TYPE_DOUBLE;
		if (realFieldType == "System.String")	return FIELD_TYPE_STRING;
		return null;
	}
	
	/*
	private byte[] serializeObject(object serializableObject) {
		// Returns a serializable object as a byte array

		BinaryFormatter formatter = new BinaryFormatter();
		using(memoryStream) {
			formatter.Serialize(memoryStream, serializableObject);
		}
		//return Convert.ToBase64String(memoryStream.ToArray());
		return memoryStream.ToArray();
	}

	private T deserializeObject<T>(byte[] source) {
		// Creates a serializable object from a string
		BinaryFormatter formatter = new BinaryFormatter();
		return formatter.Deserialize(source);
	}
	
	private void saveToPlayerPrefs() {
		// Save everything with regular PlayerPrefs

		// Write primitives
		PlayerPrefs.SetString(getKeyForName(FIELD_NAME_PRIMITIVES), getPrimitivesAsString())

		// Write object list
		var objectKeys = getListOfObjectKeys();
		PlayerPrefs.SetString(getKeyForName(FIELD_NAME_OBJECTS), string.Join(SERIALIZATION_SEPARATOR, objectKeys))
		foreach (var objectKey in ObjectKeys) {
			PlayerPrefs.SetString(getKeyForName(objectKey), Convert.ToBase64String(data[objectKey]))
		}
		
		// Save all fields
		PlayerPrefs.Save();
	}
	 * */

	private string getSavedString(string name) {
		// Reads a string that has been saved previously
		#if UNITY_WEBPLAYER
			// Using PlayerPrefs
			return PlayerPrefs.GetString(getKeyForName(name));
		#else
			// Using a file
			return loadFileAsString(getKeyForName(name));
		#endif
	}

	private void setSavedString(string name, string value) {
		// Save a string to some persistent data system
		#if UNITY_WEBPLAYER
			// Using PlayerPrefs
			PlayerPrefs.SetString(getKeyForName(name), value);
		#else
			// Using a file
			saveStringToFile(getKeyForName(name), value);
		#endif
	}

	/*
	private void loadFromPlayerPrefs() {
		// Read primitives
		string primitiveData = PlayerPrefs.GetString(getKeyForName(FIELD_NAME_PRIMITIVES));
		string[] fields = primitiveData.Split(new string[] {separator});

		for (int i = 0; i < fields.Count; i += 3) {
			data.Add(decodeString(fields[i]), getValueAsType(decodeString(fields[i+1]), decodeString(fields[i+2])));
		}
		
		// Read object keys
		string objectsData = PlayerPrefs.GetString(getKeyForName(FIELD_NAME_OBJECTS));
		string[] objectKeys = objectsData.Split(new string[] {separator});
		string objectKey;

		// Read actual object list
		for (int i = 0; i < fields.Count; i += 3) {
			objectKey = objectKeys[i];
			data.Add(objectKey, Convert.FromBase64String(PlayerPrefs.GetString(getKeyForName(objectKey)));
		}
	}

	private void saveToFiles() {
		// Save everything with binary files
		
		// Write primitives
		saveFile(getKeyForName(FIELD_NAME_PRIMITIVES), getPrimitivesAsString())

		// Write object list
		var objectKeys = getListOfObjectKeys();
		saveFile(getKeyForName(FIELD_NAME_OBJECTS), string.Join(SERIALIZATION_SEPARATOR, objectKeys))
		foreach (var objectKey in ObjectKeys) {
			saveFile(getKeyForName(objectKey), data[objectKey])
		}
	}
	
	private loadFromFiles() {
		// Load from binary files
		// Read primitives
		string primitiveData = loadFileAsString(getKeyForName(FIELD_NAME_PRIMITIVES));
		string[] fields = primitiveData.Split(new string[] {separator});

		for (int i = 0; i < fields.Count; i += 3) {
			data.Add(decodeString(fields[i]), getValueAsType(decodeString(fields[i+1]), decodeString(fields[i+2])));
		}
		
		// Read object keys
		string objectsData = loadFileAsString(getKeyForName(FIELD_NAME_OBJECTS));
		string[] objectKeys = objectsData.Split(new string[] {separator});
		string objectKey;

		// Read actual object list
		for (int i = 0; i < fields.Count; i += 3) {
			objectKey = objectKeys[i];
			data.Add(objectKey, loadFile(PlayerPrefs.GetString(getKeyForName(objectKey)));
		}
	}
	*/

	private void saveStringToFile(string filename, string content) {
		BinaryFormatter formatter = new BinaryFormatter();
		FileStream file = File.Create(Application.persistentDataPath + "/" + filename);
		
		formatter.Serialize(file, content);
		file.Close();
	}

	private string loadFileAsString(string filename) {
		object obj = loadFile(filename);
		return obj == null ? "" : (string) obj;
	}

	private object loadFile(string filename) {
		string filePath = Application.persistentDataPath + "/" + filename;
		if (File.Exists(filePath)) {
			BinaryFormatter formatter = new BinaryFormatter();
			FileStream file = File.Open(filePath, FileMode.Open);
			object content = formatter.Deserialize(file);
			file.Close();
			return content;
		}
		return null;
	}

	/*
	private string getPrimitivesAsString() {
		// Create a list of all fields (key, value, type) as a string separated by SERIALIZATION_SEPARATOR
		IDictionaryEnumerator enumerator = data.GetEnumerator();
		System.Text.StringBuilder primitiveDataBuilder = new System.Text.StringBuilder();
		bool started = false;
		string primitiveFieldType, fieldType;

		while (enumerator.MoveNext()) {
			primitiveFieldType = enumerator.Value.GetType();
			fieldType = "";

			// TODO: use "is" ? 
			if (primitiveFieldType == "System.Boolean")	{
				fieldType = FIELD_TYPE_BOOLEAN;
			} else if (primitiveFieldType == "System.Int32") {
				fieldType = FIELD_TYPE_INT;
			} else if (primitiveFieldType == "System.Int64") {
				fieldType = FIELD_TYPE_LONG;
			} else if (primitiveFieldType == "System.Single") {
				fieldType = FIELD_TYPE_FLOAT;
			} else if (primitiveFieldType == "System.Double") {
				fieldType = FIELD_TYPE_DOUBLE;
			} else if (primitiveFieldType == "System.String") {
				fieldType = FIELD_TYPE_STRING;
			} else {
				continue;
			}
			
			// Primitive
			if (started) sb.Append(SERIALIZATION_SEPARATOR);
			primitiveDataBuilder.Append(encodeString(enumerator.Key.ToString());
			primitiveDataBuilder.Append(SERIALIZATION_SEPARATOR);
			primitiveDataBuilder.Append(encodeString(enumerator.Value.ToString());
			primitiveDataBuilder.Append(SERIALIZATION_SEPARATOR);
			primitiveDataBuilder.Append(encodeString(fieldType);
			started = true;
		}
		
		return primitiveDataBuilder.ToString();
	}
	
	private List<string> getListOfObjectKeys() {
		// Returns a list of all the keys that contain objects
		var objectKeys = new List<string>();
		
		IDictionaryEnumerator enumerator = data.GetEnumerator();
		while (enumerator.MoveNext()) {
			// TODO: use "is" ? 
			if (enumerator.Value.GetType().Name == "Byte[]") objectKeys.Add(encodeString(enumerator.Key.ToString()));
		}
		
		return objectKeys;
	}
	*/

}
