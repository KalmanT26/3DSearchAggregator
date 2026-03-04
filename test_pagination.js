const fetchList = async (url) => {
    const res = await fetch(url);
    const json = await res.json();
    return json.results.map(r => r.likeCount);
};

(async () => {
    const p1 = await fetchList('http://localhost:5000/api/models/search?q=dragon&page=1&pageSize=4&sortBy=likes&sources=Printables');
    const p2 = await fetchList('http://localhost:5000/api/models/search?q=dragon&page=2&pageSize=4&sortBy=likes&sources=Printables');
    
    console.log("Printables P1:", p1);
    console.log("Printables P2:", p2);

    const m1 = await fetchList('http://localhost:5000/api/models/search?q=dragon&page=1&pageSize=4&sortBy=likes&sources=MyMiniFactory');
    const m2 = await fetchList('http://localhost:5000/api/models/search?q=dragon&page=2&pageSize=4&sortBy=likes&sources=MyMiniFactory');
    
    console.log("MMF P1:", m1);
    console.log("MMF P2:", m2);

    const all1 = await fetchList('http://localhost:5000/api/models/search?q=dragon&page=1&pageSize=4&sortBy=likes');
    const all2 = await fetchList('http://localhost:5000/api/models/search?q=dragon&page=2&pageSize=4&sortBy=likes');
    
    console.log("ALL P1:", all1);
    console.log("ALL P2:", all2);
})();
