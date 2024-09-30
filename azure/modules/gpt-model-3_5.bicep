//doesn't work but don't know why
param deploymentName string
param skuName string = 'Standard'
param skuCapacity int = 1
param aiResourceName string

resource aiResource 'Microsoft.CognitiveServices/accounts@2024-06-01-preview' existing = {
  name: aiResourceName
}

resource gptModel 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: aiResource
  name: deploymentName
  sku: {
    name: skuName
    capacity: skuCapacity
  }
  properties: {
    model: {
      name: 'gpt-3.5-turbo'
      version: '2022-04-01'
    }
  }
}
