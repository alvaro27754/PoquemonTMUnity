using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BattleHUD : MonoBehaviour
{
    public Text pokemonName;
    public Text pokemonLevel;
    public HealthBar healthbar;
    public GameObject statusBox;
    public GameObject expBar;
    
    private Pokemon _pokemon;
    
    public void SetPokemonData(Pokemon pokemon)
    {
        _pokemon = pokemon;
        
        pokemonName.text = pokemon.Base.Name;
        SetLevelText();
        healthbar.SetHP(_pokemon);
        SetExp();

        StartCoroutine(UpdatePokemonData());
        SetStausConditionData();
        _pokemon.OnStatusConditionChanged += SetStausConditionData;
    }

    public IEnumerator UpdatePokemonData()
    {
        if (_pokemon.HasHPChanged)
        {
            yield return healthbar.SetSmoothHP(_pokemon);
            //StartCoroutine(DecreaseHealthPoints(oldHPVal));
            //yield return null;
            _pokemon.HasHPChanged = false;
        }
    }

    /*private IEnumerator DecreaseHealthPoints(int oldHPVal)
    {
        while (oldHPVal>_pokemon.HP)
        {
            oldHPVal--;
            pokemonHealth.text = $"{oldHPVal}/{_pokemon.MaxHP}";
            yield return new WaitForSeconds(0.1f);
        }
        pokemonHealth.text = $"{_pokemon.HP}/{_pokemon.MaxHP}";
    }*/


    public void SetExp()
    {
        if (expBar==null)
        {
            return;
        }
        
        expBar.transform.localScale = new Vector3(NormalizedExp(), 1, 1);
    }

    public IEnumerator SetExpSmooth(bool needsToResetBar = false)
    {
        if (expBar==null)
        {
            yield break;
        }

        if (needsToResetBar)
        {
            expBar.transform.localScale = new Vector3(0, 1, 1);
        }
        
        yield return expBar.transform.DOScaleX(NormalizedExp(), 2f).WaitForCompletion();
    }

    float NormalizedExp()
    {
        float currentLevelExp = _pokemon.Base.GetNecessaryExpForLevel(_pokemon.Level);
        float nextLevelExp = _pokemon.Base.GetNecessaryExpForLevel(_pokemon.Level+1);
        float normalizedExp = (_pokemon.Experience - currentLevelExp) / (nextLevelExp - currentLevelExp);
        
        Debug.Log($"current {currentLevelExp}; next {nextLevelExp}, norml {normalizedExp}");
        return Mathf.Clamp01(normalizedExp);
    }

    public void SetLevelText()
    {
        pokemonLevel.text = $"Lv {_pokemon.Level}";
    }

    void SetStausConditionData()
    {
        if (_pokemon.StatusCondition == null)
        {
            statusBox.SetActive(false);
        }
        else
        {
            statusBox.SetActive(true);
            statusBox.GetComponent<Image>().color = 
                ColorManager.StatusConditionColor.GetColorFromStatusCondition
                (_pokemon.StatusCondition.ID);
            statusBox.GetComponentInChildren<Text>().text = _pokemon.StatusCondition.ID.ToString().ToUpper();
        }
    }
}
