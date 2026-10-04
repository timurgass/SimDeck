package dev.simdeck

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import java.text.Collator
import java.util.Locale

internal fun fs25Alphabet() = Collator.getInstance(Locale.forLanguageTag("ru")).apply { strength=Collator.PRIMARY }

@Composable internal fun Fs25Selector(title:String, choices:List<Pair<String,String>>, selected:String?, onSelect:(String?)->Unit) {
    var open by remember { mutableStateOf(false) }
    var query by remember { mutableStateOf("") }
    OutlinedButton(onClick={query="";open=true},modifier=Modifier.fillMaxWidth()) {
        Text("$title: ${choices.firstOrNull { it.first==selected }?.second ?: "Все"} ▾")
    }
    if(open) AlertDialog(onDismissRequest={open=false},title={Text(title)},text={
        Column(verticalArrangement=Arrangement.spacedBy(8.dp)) {
            OutlinedTextField(value=query,onValueChange={query=it.take(80)},singleLine=true,label={Text("Поиск по названию")},modifier=Modifier.fillMaxWidth())
            val visible=choices.filter { query.isBlank() || it.second.contains(query.trim(),ignoreCase=true) }
            LazyColumn(Modifier.fillMaxWidth().heightIn(max=320.dp)) {
                item { TextButton(onClick={onSelect(null);open=false},modifier=Modifier.fillMaxWidth()) { Text(if(selected==null) "✓ Все" else "Все") } }
                items(visible,key={it.first}) { (id,name) -> TextButton(onClick={onSelect(id);open=false},modifier=Modifier.fillMaxWidth()) { Text(if(id==selected) "✓ $name" else name) } }
                if(visible.isEmpty()) item { Text("По этому запросу ничего не найдено") }
            }
        }
    },confirmButton={TextButton(onClick={open=false}) { Text("Закрыть") }})
}
