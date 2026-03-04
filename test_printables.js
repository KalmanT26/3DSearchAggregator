const query = `{ __type(name: "SearchChoicesEnum") { enumValues { name } } }`;
fetch('https://api.printables.com/graphql/', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query })
}).then(r => r.json()).then(j => console.log(JSON.stringify(j)));
