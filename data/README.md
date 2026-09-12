# Dataset

This project uses the **Global Air Pollution Dataset** published on Kaggle.

## Source

- Dataset: Global Air Pollution Dataset
- Publisher: Hasib Al Muzdadid
- Kaggle page: https://www.kaggle.com/datasets/hasibalmuzdadid/global-air-pollution-dataset/data
- Expected local filename: `global_air_pollution_dataset.csv`

## File summary

- Rows: 23,464
- Columns: 12
- Distinct country values: 176
- Overall AQI range in the supplied CSV: 6-500
- SHA-256 of the project copy: `7a96a2f59a97af33108957efa75c95a80c6a9271d244117d5d4869846e3d7f60`

## Columns

- Country
- City
- AQI Value
- AQI Category
- CO AQI Value
- CO AQI Category
- Ozone AQI Value
- Ozone AQI Category
- NO2 AQI Value
- NO2 AQI Category
- PM2.5 AQI Value
- PM2.5 AQI Category

## Repository note

The raw CSV is not required to be committed to the public repository. The Kaggle page currently lists the dataset license as **Other (specified in description)**, so redistribution permission should be confirmed before publishing the full file.

To run the import locally, download the dataset from Kaggle and place it in this folder as:

```text
data/global_air_pollution_dataset.csv
```
